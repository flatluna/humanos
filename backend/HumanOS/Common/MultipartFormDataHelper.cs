using System.Text;
using System.Text.RegularExpressions;

namespace HumanOS.Common;

public sealed class ParsedMultipartFile
{
    public string Name { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public byte[] Data { get; set; } = [];
}

public sealed class ParsedMultipartFormData
{
    public Dictionary<string, string> Fields { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<ParsedMultipartFile> Files { get; } = [];
}

/// <summary>
/// Lightweight, reliable multipart/form-data parser for Azure Functions isolated worker.
/// </summary>
public static class MultipartFormDataHelper
{
    public static async Task<ParsedMultipartFormData> ParseAsync(
        Stream bodyStream,
        string contentTypeHeader,
        CancellationToken cancellationToken = default)
    {
        var result = new ParsedMultipartFormData();

        // Extract boundary parameter from Content-Type
        var boundaryMatch = Regex.Match(contentTypeHeader, @"boundary=(?:""([^""]+)""|([^;]+))", RegexOptions.IgnoreCase);
        if (!boundaryMatch.Success)
        {
            return result;
        }

        var boundary = boundaryMatch.Groups[1].Success ? boundaryMatch.Groups[1].Value : boundaryMatch.Groups[2].Value.Trim();
        var delimiterBytes = Encoding.UTF8.GetBytes($"--{boundary}");

        using var memoryStream = new MemoryStream();
        await bodyStream.CopyToAsync(memoryStream, cancellationToken);
        var bodyBytes = memoryStream.ToArray();

        var boundaryIndices = FindBytePatterns(bodyBytes, delimiterBytes);
        if (boundaryIndices.Count < 2)
        {
            return result;
        }

        for (var i = 0; i < boundaryIndices.Count - 1; i++)
        {
            var start = boundaryIndices[i] + delimiterBytes.Length;
            var end = boundaryIndices[i + 1];

            // Strip leading \r\n
            if (start + 2 <= end && bodyBytes[start] == '\r' && bodyBytes[start + 1] == '\n')
            {
                start += 2;
            }
            else if (start + 1 <= end && bodyBytes[start] == '\n')
            {
                start += 1;
            }

            // Strip trailing \r\n
            if (end - 2 >= start && bodyBytes[end - 2] == '\r' && bodyBytes[end - 1] == '\n')
            {
                end -= 2;
            }
            else if (end - 1 >= start && bodyBytes[end - 1] == '\n')
            {
                end -= 1;
            }

            if (start >= end) continue;

            // Find headers-body separator: \r\n\r\n or \n\n
            var headerEndIndex = -1;
            var bodyStartIndex = -1;

            for (var j = start; j < end - 3; j++)
            {
                if (bodyBytes[j] == '\r' && bodyBytes[j + 1] == '\n' && bodyBytes[j + 2] == '\r' && bodyBytes[j + 3] == '\n')
                {
                    headerEndIndex = j;
                    bodyStartIndex = j + 4;
                    break;
                }
            }

            if (headerEndIndex == -1)
            {
                for (var j = start; j < end - 1; j++)
                {
                    if (bodyBytes[j] == '\n' && bodyBytes[j + 1] == '\n')
                    {
                        headerEndIndex = j;
                        bodyStartIndex = j + 2;
                        break;
                    }
                }
            }

            if (headerEndIndex == -1 || bodyStartIndex >= end) continue;

            var headerText = Encoding.UTF8.GetString(bodyBytes, start, headerEndIndex - start);
            var contentDispositionMatch = Regex.Match(headerText, @"Content-Disposition:\s*form-data;\s*name=""([^""]+)""(?:;\s*filename=""([^""]*)"")?", RegexOptions.IgnoreCase);

            if (!contentDispositionMatch.Success) continue;

            var fieldName = contentDispositionMatch.Groups[1].Value;
            var fileName = contentDispositionMatch.Groups[2].Success ? contentDispositionMatch.Groups[2].Value : null;

            var contentTypeMatch = Regex.Match(headerText, @"Content-Type:\s*([^\r\n;]+)", RegexOptions.IgnoreCase);
            var partContentType = contentTypeMatch.Success ? contentTypeMatch.Groups[1].Value.Trim() : "application/octet-stream";

            var partDataLength = end - bodyStartIndex;
            var partData = new byte[partDataLength];
            Array.Copy(bodyBytes, bodyStartIndex, partData, 0, partDataLength);

            if (!string.IsNullOrEmpty(fileName))
            {
                result.Files.Add(new ParsedMultipartFile
                {
                    Name = fieldName,
                    FileName = fileName,
                    ContentType = partContentType,
                    Data = partData
                });
            }
            else
            {
                var fieldValue = Encoding.UTF8.GetString(partData);
                result.Fields[fieldName] = fieldValue;
            }
        }

        return result;
    }

    private static List<int> FindBytePatterns(byte[] source, byte[] pattern)
    {
        var positions = new List<int>();
        if (source.Length < pattern.Length) return positions;

        for (var i = 0; i <= source.Length - pattern.Length; i++)
        {
            var match = true;
            for (var j = 0; j < pattern.Length; j++)
            {
                if (source[i + j] != pattern[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                positions.Add(i);
                i += pattern.Length - 1;
            }
        }

        return positions;
    }
}
