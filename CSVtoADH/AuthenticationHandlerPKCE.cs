using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace CSVtoADH
{
    /// <summary>
    /// Handle authentication with Identity Server.
    /// </summary>
    public class AuthenticationHandlerPKCE : DelegatingHandler
    {
        private string _accessToken;
        private DateTimeOffset _expiration;

        public AuthenticationHandlerPKCE(string tenantId, string clientId, string resource = "https://uswe.datahub.connect.aveva.com")
        {
            _expiration = GetTimeZoneAgnosticOffset();

            TenantId = tenantId;
            ClientId = clientId;
            AuthorizationCode.AdhAddress = resource;
            AuthorizationCode.RedirectHost = "https://127.0.0.1";
            AuthorizationCode.RedirectPort = 54567;
            AuthorizationCode.RedirectPath = "signin-oidc";

            if (SystemBrowser.OpenBrowser == null)
                SystemBrowser.OpenBrowser = new OpenSystemBrowser();
        }

        private string ClientId { get; set; }
        private string TenantId { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (_accessToken == null || _expiration.AddSeconds(5) < DateTime.Now)
            {
                (_accessToken, _expiration) =
                    AuthorizationCode.GetAuthorizationCodeFlowAccessToken(ClientId, TenantId);
            }

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            return base.SendAsync(request, cancellationToken);
        }

        private static DateTimeOffset GetTimeZoneAgnosticOffset()
        {
            // MinValue will be converted to UTC, which for time zones ahead of UTC will result in a value less than MinValue.
            // Adding this offset to make sure the conversion works for all time zones.
            var utcOffset = TimeZoneInfo.Local.BaseUtcOffset;
            return DateTime.MinValue + utcOffset;
        }
    }
}
