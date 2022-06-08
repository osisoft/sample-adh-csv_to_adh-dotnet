namespace CSVtoADHTest
{
    using System.Threading.Tasks;
    using Xunit;

    /// <summary>
    /// Holds all of the tests.
    /// </summary>
    public class UnitTests
    {
        /// <summary>
        /// Simple E2E test.
        /// </summary>
        [Fact]
        public async Task CSVtoADHTest()
        {
            CSVtoADH.SystemBrowser.OpenBrowser = new OpenTestBrowser();

            Assert.True(await CSVtoADH.Program.RunAsync(test: true).ConfigureAwait(false));
        }
    }
}
