using System.Net;
using System.Text.Json;

namespace Tensu.Api.Infrastructure;

/// <summary>
/// Parses and evaluates API key IP whitelists, supporting IPv4/IPv6 single addresses and CIDR blocks.
/// </summary>
public class IpWhitelistService
{
    public string? GetClientIp(HttpContext httpContext)
    {
        // 1. X-Forwarded-For: client, proxy1, proxy2
        var forwarded = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            var first = forwarded.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .FirstOrDefault(s => !string.IsNullOrEmpty(s) && !string.Equals(s, "unknown", StringComparison.OrdinalIgnoreCase));

            if (TryParseIpEndpoint(first, out var ep) && ep != null)
                return ep.Address.ToString();
        }

        // 2. X-Real-IP
        var realIp = httpContext.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (TryParseIpEndpoint(realIp, out var realEp) && realEp != null)
            return realEp.Address.ToString();

        // 3. Connection remote address
        var remote = httpContext.Connection.RemoteIpAddress;
        return remote?.ToString();
    }

    public bool IsAllowed(string? ipWhitelistJson, string? clientIp)
    {
        if (string.IsNullOrWhiteSpace(ipWhitelistJson)) return true;

        List<string>? entries = null;
        try
        {
            entries = JsonSerializer.Deserialize<List<string>>(ipWhitelistJson);
        }
        catch { }

        if (entries == null || entries.Count == 0) return true;
        if (string.IsNullOrWhiteSpace(clientIp)) return false;

        if (!IPAddress.TryParse(clientIp, out var clientAddress)) return false;

        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry)) continue;

            var trimmed = entry.Trim();
            if (trimmed.Contains('/'))
            {
                if (IsInCidr(clientAddress, trimmed)) return true;
            }
            else
            {
                if (IPAddress.TryParse(trimmed, out var allowed) && allowed.Equals(clientAddress)) return true;
            }
        }

        return false;
    }

    private static bool IsInCidr(IPAddress address, string cidr)
    {
        var parts = cidr.Split('/');
        if (parts.Length != 2) return false;
        if (!IPAddress.TryParse(parts[0], out var networkAddress)) return false;
        if (!int.TryParse(parts[1], out var prefixLength)) return false;

        if (address.AddressFamily != networkAddress.AddressFamily) return false;

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return IsInCidrIPv4(address, networkAddress, prefixLength);
        }

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            return IsInCidrIPv6(address, networkAddress, prefixLength);
        }

        return false;
    }

    private static bool IsInCidrIPv4(IPAddress address, IPAddress network, int prefixLength)
    {
        var addressBytes = address.GetAddressBytes();
        var networkBytes = network.GetAddressBytes();
        if (addressBytes.Length != 4 || networkBytes.Length != 4) return false;

        var mask = prefixLength == 0 ? 0u : uint.MaxValue << (32 - prefixLength);
        var addressValue = (uint)addressBytes[0] << 24 | (uint)addressBytes[1] << 16 | (uint)addressBytes[2] << 8 | addressBytes[3];
        var networkValue = (uint)networkBytes[0] << 24 | (uint)networkBytes[1] << 16 | (uint)networkBytes[2] << 8 | networkBytes[3];

        return (addressValue & mask) == (networkValue & mask);
    }

    private static bool IsInCidrIPv6(IPAddress address, IPAddress network, int prefixLength)
    {
        var addressBytes = address.GetAddressBytes();
        var networkBytes = network.GetAddressBytes();
        if (addressBytes.Length != 16 || networkBytes.Length != 16) return false;
        if (prefixLength < 0 || prefixLength > 128) return false;

        var fullBytes = prefixLength / 8;
        for (var i = 0; i < fullBytes; i++)
        {
            if (addressBytes[i] != networkBytes[i]) return false;
        }

        var remainingBits = prefixLength % 8;
        if (remainingBits == 0) return true;

        var mask = (byte)(0xFF << (8 - remainingBits));
        return (addressBytes[fullBytes] & mask) == (networkBytes[fullBytes] & mask);
    }

    private static bool TryParseIpEndpoint(string? value, out IPEndPoint? endpoint)
    {
        endpoint = null;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var trimmed = value.Trim();

        // IPv6 with brackets and optional port, e.g. [::1]:8080
        if (trimmed.StartsWith('['))
        {
            var bracketEnd = trimmed.IndexOf(']');
            if (bracketEnd > 0)
            {
                var ipPart = trimmed[1..bracketEnd];
                if (IPAddress.TryParse(ipPart, out var ip))
                {
                    if (trimmed.Length > bracketEnd + 2 && trimmed[bracketEnd + 1] == ':' && int.TryParse(trimmed[(bracketEnd + 2)..], out var port))
                        endpoint = new IPEndPoint(ip, port);
                    else
                        endpoint = new IPEndPoint(ip, 0);
                    return true;
                }
            }
        }

        // IPv4:port or IPv6 without brackets (with port)
        if (IPEndPoint.TryParse(trimmed, out endpoint))
            return true;

        // Plain IP address
        if (IPAddress.TryParse(trimmed, out var address))
        {
            endpoint = new IPEndPoint(address, 0);
            return true;
        }

        return false;
    }
}
