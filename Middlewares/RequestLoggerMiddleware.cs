using System.Text;
using Microsoft.IO;

namespace Netcorext.Logging.AspNetCoreLogger;

public class RequestLoggerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger _logger;
    private readonly RecyclableMemoryStreamManager _recyclableMemoryStreamManager;

    public static class EventIds
    {
        public static readonly EventId RequestInfo = new EventId(10000, "RequestInfo");
    }

    public RequestLoggerMiddleware(RequestDelegate next, ILoggerFactory loggerFactory)
    {
        _next = next;
        _logger = loggerFactory.CreateLogger<RequestLoggerMiddleware>();
        _recyclableMemoryStreamManager = new RecyclableMemoryStreamManager();
    }

    public async Task Invoke(HttpContext context)
    {
        if (!_logger.IsEnabled(LogLevel.Trace))
        {
            await _next(context);

            return;
        }

        var content = $"Content is empty or Content-Type ({context.Request.ContentType}) is not support log.";

        if (context.Request.ContentType != null
         && (context.Request.ContentType.Contains("text/")
          || context.Request.ContentType.Contains("application/json")
          || context.Request.ContentType.Contains("application/xml")
          || context.Request.ContentType.Contains("application/x-www-form-urlencoded")))
        {
            context.Request.EnableBuffering();

            var requestStream = _recyclableMemoryStreamManager.GetStream();

            await context.Request.BodyReader.CopyToAsync(requestStream);

            content = await ReadStreamAsync(requestStream, false);

            context.Request.Body.Seek(0, SeekOrigin.Begin);
        }

        _logger.LogTrace(EventIds.RequestInfo,
                         $"Request Information: {context.Request.Protocol} {context.Request.Method} {context.Request.Scheme}://{context.Request.Host}{context.Request.Path}{context.Request.QueryString}{Environment.NewLine}" +
                         $"Headers:{Environment.NewLine}{GetHeaders(context.Request.Headers)}" +
                         $"Content:{Environment.NewLine}{content}");

        await _next(context);
    }

    private static async Task<string> ReadStreamAsync(Stream stream, bool enableDispose = true)
    {
        stream.Seek(0, SeekOrigin.Begin);

        if (!enableDispose)
        {
            return await new StreamReader(stream).ReadToEndAsync();
        }

        using var streamReader = new StreamReader(stream);

        return await streamReader.ReadToEndAsync();
    }

    private static string GetHeaders(IHeaderDictionary headers)
    {
        var builder = new StringBuilder();

        foreach (var header in headers)
        {
            builder.Append(header.Key);
            builder.Append(": ");

            foreach (var value in (IEnumerable<object>)header.Value)
            {
                builder.Append(value);
                builder.Append(", ");
            }

            // Remove the extra ', '
            builder.Remove(builder.Length - 2, 2);
            builder.AppendLine();
        }

        return builder.ToString();
    }
}