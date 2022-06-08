using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CsvHelper;
using Microsoft.Extensions.Configuration;
using OSIsoft.Data;
using OSIsoft.Data.Reflection;

namespace CSVtoADH
{
    public static class Program
    {
        private const string Stream1Id = "CSVtoADHStream_1";
        private const string Stream2Id = "CSVtoADHStream_2";
        private const string TypeId = "TemperatureReadings";

        private static bool _successful;
        private static List<string> _errors = new List<string>();
        private static List<TemperatureReadingsWithIds> _dataList;
        private static IEnumerable<string> _streamsIdsToSendTo;
        private static ISdsDataService _dataService;
        private static ISdsMetadataService _metaService;
        private static IConfiguration _configuration;

        private static bool CreateStreams { get; set; } = true;

        public static void Main(string[] args)
        {
            string fileLocationIn = "datafile.csv";
            if (args != null && args.Length > 0)
            {
                fileLocationIn = args[0];
            }

            RunAsync(fileLocation: fileLocationIn).GetAwaiter().GetResult();
        }

        public static async Task<bool> RunAsync(bool test = false, string fileLocation = "datafile.csv")
        {
            _successful = true;

            try
            {
                // Import CSV data using the TemperatureReadingsWithIds class as format
                using (StreamReader reader = new (fileLocation))
                using (CsvReader csv = new (reader, CultureInfo.InvariantCulture))
                {
                    _dataList = csv.GetRecords<TemperatureReadingsWithIds>().ToList();
                }

                // Get Stream Ids to use when sending data
                _streamsIdsToSendTo = _dataList.Select(dataEntry => dataEntry.StreamId).Distinct();

                // Get Configuration information about where this is sending to
                _configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json")
                    .AddJsonFile("appsettings.test.json", optional: true)
                    .Build();

                string tenantId = _configuration["TenantId"];
                string namespaceId = _configuration["NamespaceId"];
                string resource = _configuration["Resource"];
                string clientId = _configuration["ClientId"];

                if (!test)
                {
                    SystemBrowser.OpenBrowser = new OpenSystemBrowser();
                }
                else
                {
                    SystemBrowser.Password = _configuration["Password"];
                    SystemBrowser.UserName = _configuration["Username"];
                }

                (_configuration as ConfigurationRoot).Dispose();

                // Setup authentication to Data Hub
                AuthenticationHandlerPKCE authenticationHandler = new (tenantId, clientId, resource);

                // Services used to communicate with the Sequential Data Store
                SdsService sdsService = new (new Uri(resource), authenticationHandler);
                _dataService = sdsService.GetDataService(tenantId, namespaceId);
                _metaService = sdsService.GetMetadataService(tenantId, namespaceId);

                if (CreateStreams)
                {
                    Console.WriteLine("Creating Type");
                    SdsType temperatureReadingsType = SdsTypeBuilder.CreateSdsType<TemperatureReadings>();
                    temperatureReadingsType.Id = TypeId;
                    temperatureReadingsType = await _metaService.GetOrCreateTypeAsync(temperatureReadingsType).ConfigureAwait(false);

                    Console.WriteLine("Creating Streams");
                    foreach (string streamId in _streamsIdsToSendTo)
                    {
                        _ = await _metaService.GetOrCreateStreamAsync(
                                new SdsStream() 
                                { 
                                    Id = streamId, 
                                    TypeId = temperatureReadingsType.Id,
                                })
                            .ConfigureAwait(false);
                    }
                }

                Console.WriteLine("Sending Data");
                foreach (string streamId in _streamsIdsToSendTo)
                {
                    // Get a List of values for this Stream formatted as TemperatureReadings
                    List<TemperatureReadings> valueToSend = _dataList.Where(dataEntry => dataEntry.StreamId == streamId)
                                              .Select(dataEntry => new TemperatureReadings(dataEntry))
                                              .ToList();

                    await _dataService.InsertValuesAsync(streamId, valueToSend).ConfigureAwait(false);
                }

                if (test)
                {
                    await EnsureValuesInsertedAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _successful = false;
                _errors.Add($"{nameof(Main)} Error: {ex.Message}");
            }
            finally
            {
                if (test)
                {
                    if (CreateStreams)
                    {
                        Console.WriteLine("Deleting Streams");
                        await RunSilently(_metaService.DeleteStreamAsync, Stream1Id).ConfigureAwait(false);
                        await RunSilently(_metaService.DeleteStreamAsync, Stream2Id).ConfigureAwait(false);

                        Console.WriteLine("Deleting Types");
                        await RunSilently(_metaService.DeleteTypeAsync, TypeId).ConfigureAwait(false);

                        // Verify successful deletes by ensuring GET calls throw
                        await EnsureThrowsAsync(_metaService.GetStreamAsync, Stream1Id).ConfigureAwait(false);
                        await EnsureThrowsAsync(_metaService.GetStreamAsync, Stream2Id).ConfigureAwait(false);
                        await EnsureThrowsAsync(_metaService.GetTypeAsync, TypeId).ConfigureAwait(false);
                    }
                    else
                    {
                        // Delete Data
                        Console.WriteLine("Deleting Data");
                        await DeleteValuesAsync().ConfigureAwait(false);
                    }
                }
            }

            if (_successful)
            {
                return true;
            }
            else
            {
                string errors = string.Empty;
                _errors.ForEach(e => errors += e + Environment.NewLine);

                throw new Exception($"Encountered Error(s): {errors}");
            }
        }

        /// <summary>
        /// Run <paramref name="toRun"/> with argument <paramref name="arg"/> and swallow any exception.
        /// </summary>
        /// <param name="toRun">The method to run.</param>
        /// <param name="arg">The method argument/</param>
        private static async Task RunSilently(Func<string, Task> toRun, string arg)
        {
            try
            {
                await toRun(arg).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{nameof(RunSilently)} Error: Running {toRun.Method.Name} with value {arg}:" + ex.Message);
                
                // Swallowing exception
            }
        }

        /// <summary>
        /// Run <paramref name="toRun"/> with argument <paramref name="arg"/> ensuring an exception to be thrown that will in turn be swallowed.
        /// </summary>
        /// <param name="toRun">The method to run.</param>
        /// <param name="arg">The method argument/</param>
        private static async Task EnsureThrowsAsync(Func<string, Task> toRun, string arg)
        {
            try
            {
                await toRun(arg).ConfigureAwait(false);

                _successful = false;
                _errors.Add($"{nameof(EnsureThrowsAsync)} Error: Expected {toRun.Method.Name} with value {arg} to throw an exception");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{nameof(EnsureThrowsAsync)}: Swallowed exception {ex.Message} when running {toRun.Method.Name} with value {arg}");
            }
        }

        /// <summary>
        /// Ensure that all streams have data inserted.
        /// </summary>
        private static async Task EnsureValuesInsertedAsync()
        {
            foreach (string streamId in _streamsIdsToSendTo)
            {
                TemperatureReadings lastVal = await _dataService.GetLastValueAsync<TemperatureReadings>(streamId).ConfigureAwait(false);
                if (lastVal == null)
                {
                    _successful = false;
                    _errors.Add($"{nameof(EnsureValuesInsertedAsync)} Error: Value for {streamId} was not found");
                }
            }
        }

        /// <summary>
        /// Delete values inserted on all streams.
        /// </summary>
        private static async Task DeleteValuesAsync()
        {
            foreach (string streamId in _streamsIdsToSendTo)
            {
                try
                {
                    IEnumerable<string> indicesToDelete = _dataList.Select(reading => reading.Timestamp.ToString(CultureInfo.InvariantCulture));
                    
                    // If any values are present
                    IEnumerable<TemperatureReadings> currentValues = await _dataService.GetValuesAsync<TemperatureReadings>(streamId, indicesToDelete).ConfigureAwait(false);
                    if (currentValues.Any())
                    {
                        await _dataService.RemoveValuesAsync(streamId, indicesToDelete).ConfigureAwait(false);

                        // Read back values, ensuring that they were deleted
                        IEnumerable<TemperatureReadings> remainingValues = await _dataService.GetValuesAsync<TemperatureReadings>(streamId, indicesToDelete).ConfigureAwait(false);
                        if (remainingValues.Any())
                        {
                            _successful = false;
                            _errors.Add($"{nameof(DeleteValuesAsync)} Error: Stream with Id {streamId} still contains values for provided indices.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _successful = false;
                    _errors.Add($"{nameof(DeleteValuesAsync)} Error: Deleting Stream with Id {streamId} failed with the message: " + ex.Message);
                }
            }
        }
    }
}
