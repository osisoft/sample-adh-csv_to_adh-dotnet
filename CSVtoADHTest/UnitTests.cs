namespace CSVtoADHTest
{
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
        public void CSVtoADHTest()
        {
            CSVtoADH.SystemBrowser.OpenBrowser = new OpenTestBrowser();
            Assert.True(CSVtoADH.Program.MainAsync(true).Result);
        }
    }
}