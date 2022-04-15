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
        private const string Stream1ID = "stream1";
        private const string Stream2ID = "stream2";
        private const string TypeID = "TemperatureReadings";

        private static Exception _toThrow;
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

            MainAsync(fileLocation: fileLocationIn).GetAwaiter().GetResult();
        }

        public static async Task<bool> MainAsync(bool test = false, string fileLocation = "datafile.csv")
        {
            try
            {
                // Import data in.  Use csv reader and custom class to make it simple
                using (StreamReader reader = new (fileLocation))
                using (CsvReader csv = new (reader, CultureInfo.InvariantCulture))
                {
                    _dataList = csv.GetRecords<TemperatureReadingsWithIds>().ToList();
                }

                // Use Linq to get the distinct StreamIds we need.  
                _streamsIdsToSendTo = _dataList.Select(dataeEntry => dataeEntry.StreamId).Distinct();

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

                // Setup access to ADH
                AuthenticationHandlerPKCE authenticationHandler = new (tenantId, clientId, resource);

                SdsService sdsService = new (new Uri(resource), authenticationHandler);
                _dataService = sdsService.GetDataService(tenantId, namespaceId);
                _metaService = sdsService.GetMetadataService(tenantId, namespaceId);

                if (CreateStreams)
                {
                    SdsType typeToCreate = SdsTypeBuilder.CreateSdsType<TemperatureReadings>();
                    typeToCreate.Id = TypeID;
                    Console.WriteLine("Creating Type");
                    await _metaService.GetOrCreateTypeAsync(typeToCreate).ConfigureAwait(false);
                    SdsStream stream1 = new () { Id = Stream1ID, TypeId = typeToCreate.Id };
                    SdsStream stream2 = new () { Id = Stream2ID, TypeId = typeToCreate.Id };
                    Console.WriteLine("Creating Stream");
                    stream1 = await _metaService.GetOrCreateStreamAsync(stream1).ConfigureAwait(false);
                    stream2 = await _metaService.GetOrCreateStreamAsync(stream2).ConfigureAwait(false);
                }

                Console.WriteLine("Sending Data");

                // Loop over each stream to send to and send the data as one call.
                foreach (string streamId in _streamsIdsToSendTo)
                {
                    // Get all of the data for this stream in a list
                    List<TemperatureReadings> valueToSend = _dataList.Where(dataEntry => dataEntry.StreamId == streamId) // gets only appropriate data for stream
                                              .Select(dataEntry => new TemperatureReadings(dataEntry)) // transforms it to the right data
                                              .ToList(); // needed in IList format for insertValues
                    await _dataService.InsertValuesAsync(streamId, valueToSend).ConfigureAwait(false);
                }

                if (test)
                {
                    // Checks to make sure values are written
                    await CheckValuesWrittenASync().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                _toThrow = ex;
            }
            finally
            {
                if (test)
                {
                    if (!CreateStreams)
                    {
                        // if we just created the data lets just remove that
                        // Do Delete
                        Console.WriteLine("Deleting Data");
                        await DeleteValuesAsync().ConfigureAwait(false);

                        // Do Delete check
                        await CheckDeletesValuesAsync().ConfigureAwait(false);
                    }
                    else
                    {
                        Console.WriteLine("Deleting Streams");

                        // if we created the types and streams, lets remove those too
                        await RunInTryCatch(_metaService.DeleteStreamAsync, Stream1ID).ConfigureAwait(false);
                        await RunInTryCatch(_metaService.DeleteStreamAsync, Stream2ID).ConfigureAwait(false);
                        Console.WriteLine("Deleting Types");
                        await RunInTryCatch(_metaService.DeleteTypeAsync, TypeID).ConfigureAwait(false);

                        // Check deletes
                        await RunInTryCatchExpectException(_metaService.GetStreamAsync, Stream1ID).ConfigureAwait(false);
                        await RunInTryCatchExpectException(_metaService.GetStreamAsync, Stream2ID).ConfigureAwait(false);
                        await RunInTryCatchExpectException(_metaService.GetTypeAsync, TypeID).ConfigureAwait(false);
                    }
                }
            }

            if (_toThrow != null)
                throw _toThrow;

            return true;
        }

        /// <summary>
        /// Use this to run a method that you don't want to stop the program if there is an exception
        /// </summary>
        /// <param name="methodToRun">The method to run.</param>
        /// <param name="value">The value to put into the method to run</param>
        private static async Task RunInTryCatch(Func<string, Task> methodToRun, string value)
        {
            try
            {
                await methodToRun(value).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Got error in {methodToRun.Method.Name} with value {value} but continued on:" + ex.Message);
                if (_toThrow == null)
                {
                    _toThrow = ex;
                }
            }
        }

        /// <summary>
        /// Use this to run a method that you don't want to stop the program if there is an exception, and you expect an exception
        /// </summary>
        /// <param name="methodToRun">The method to run.</param>
        /// <param name="value">The value to put into the method to run</param>
        private static async Task RunInTryCatchExpectException(Func<string, Task> methodToRun, string value)
        {
            try
            {
                await methodToRun(value).ConfigureAwait(false);

                Console.WriteLine($"Got error.  Expected {methodToRun.Method.Name} with value {value} to throw an error but it did not:");
            }
            catch { }
        }

        private static async Task CheckValuesWrittenASync()
        {
            foreach (string streamId in _streamsIdsToSendTo)
            {
                try
                {
                    TemperatureReadings lastVal = await _dataService.GetLastValueAsync<TemperatureReadings>(streamId).ConfigureAwait(false);
                    if (lastVal == null && _toThrow == null)
                    {
                        throw new Exception($"Value for {streamId} was not found");
                    }
                }
                catch (Exception ex)
                {
                    if (_toThrow == null)
                    {
                        _toThrow = ex;
                    }
                }
            }
        }

        private static async Task CheckDeletesValuesAsync()
        {
            foreach (string streamId in _streamsIdsToSendTo)
            {
                try
                {
                    TemperatureReadings lastVal = await _dataService.GetLastValueAsync<TemperatureReadings>(streamId).ConfigureAwait(false);
                    if (lastVal != null && _toThrow == null)
                    {
                        throw new Exception($"Value for {streamId} was found");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Got error in seeing that removed values are gone in {streamId} but continued on:" + ex.Message);
                    if (_toThrow == null)
                    {
                        _toThrow = ex;
                    }
                }
            }
        }

        private static async Task DeleteValuesAsync()
        {
            foreach (string streamId in _streamsIdsToSendTo)
            {
                try
                {
                    IEnumerable<DateTime> timeStampToDelete = _dataList.Select(o => o.Timestamp);
                    await _dataService.RemoveValuesAsync(streamId, timeStampToDelete).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Got error in removing values in {streamId} but continued on:" + ex.Message);
                    if (_toThrow == null)
                    {
                        _toThrow = ex;
                    }
                }
            }
        }
    }
}
