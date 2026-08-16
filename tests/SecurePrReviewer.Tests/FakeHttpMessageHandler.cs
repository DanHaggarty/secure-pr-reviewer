using System.Net;
using System.Text;

namespace SecurePrReviewer.Tests
{
    internal sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode StatusCode, string Body)> _responses;

        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }
        public List<HttpRequestMessage> Requests { get; } = new();
        public List<string?> RequestBodies { get; } = new();

        public FakeHttpMessageHandler(HttpStatusCode statusCode, string responseBody)
            : this(new[] { (statusCode, responseBody) })
        {
        }

        public FakeHttpMessageHandler(IEnumerable<(HttpStatusCode StatusCode, string Body)> responses)
        {
            _responses = new Queue<(HttpStatusCode, string)>(responses);
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(request);
            RequestBodies.Add(LastRequestBody);

            cancellationToken.ThrowIfCancellationRequested();

            // Once only one response remains, keep returning it for any further calls,
            // so single-response tests work regardless of how many calls they trigger.
            var (statusCode, body) = _responses.Count > 1 ? _responses.Dequeue() : _responses.Peek();

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }
    }
}
