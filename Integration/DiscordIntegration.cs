using CS2AntiCheat.Core;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CS2AntiCheat.Integration
{
    /// <summary>All data needed to render one detection embed. Filled on the main thread.</summary>
    internal struct DetectionReport
    {
        internal string PlayerName;
        internal string SteamId64;
        internal string ProfileUrl;
        internal string Detection;   // module name
        internal string Evidence;    // reason
        internal string Punishment;  // human text for the action taken
        internal string Map;
        internal string Address;     // ip:port
        internal string ServerName;  // hostname
    }

    internal class DiscordIntegration
    {
        // ----- Webhook payload shapes (snake_case to match the Discord API) -----
#pragma warning disable IDE1006 // Naming Styles
        private class WebhookData
        {
            public string username { get; set; } = "CS2AC";
            public string? content { get; set; }
            public AllowedMentions allowed_mentions { get; set; } = new();
            public List<Embed>? embeds { get; set; }
        }

        private class AllowedMentions
        {
            public List<string> parse { get; set; } = new();
            public List<string> roles { get; set; } = new();
        }

        private class Embed
        {
            public EmbedAuthor? author { get; set; }
            public int color { get; set; }
            public List<EmbedField> fields { get; set; } = new();
            public EmbedThumbnail? thumbnail { get; set; }
            public EmbedFooter? footer { get; set; }
            public string? timestamp { get; set; }
        }

        private class EmbedAuthor
        {
            public string name { get; set; } = string.Empty;
        }

        private class EmbedField
        {
            public string name { get; set; } = string.Empty;
            public string value { get; set; } = string.Empty;
            [JsonPropertyName("inline")] public bool inlineField { get; set; }
        }

        private class EmbedThumbnail
        {
            public string url { get; set; } = string.Empty;
        }

        private class EmbedFooter
        {
            public string text { get; set; } = string.Empty;
        }
#pragma warning restore IDE1006 // Naming Styles

        private const int EmbedColorRed = 0xE02424; // red bar on the left of the embed

        private static HttpClient webClient = null!;
        private static string webClientAddress = string.Empty;

        private static string mentionPrefix = string.Empty;
        private static AllowedMentions allowedMentions = new();

        internal static bool InitializeWebhook()
        {
            string url = GeneralConfig.GetGeneralConfig().Config.DiscordWebhookUrl;

            if (string.IsNullOrWhiteSpace(url) == true)
            {
                Globals.Log("Unable to initialize Webhook ('DiscordWebhookUrl' not set)");
                return false;
            }

            webClient ??= new HttpClient();
            webClientAddress = url.Trim();

            BuildMention();
            return true;
        }

        /// <summary>
        /// Builds the ping prefix + allowed_mentions from DiscordMentionRoleId:
        ///   "" -> no ping; "everyone" -> @everyone; "here" -> @here; "&lt;role id&gt;" -> that role.
        /// </summary>
        private static void BuildMention()
        {
            mentionPrefix = string.Empty;
            allowedMentions = new AllowedMentions();

            var value = GeneralConfig.GetGeneralConfig().Config.DiscordMentionRoleId?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            if (value.Equals("everyone", StringComparison.OrdinalIgnoreCase))
            {
                mentionPrefix = "@everyone ";
                allowedMentions.parse.Add("everyone");
            }
            else if (value.Equals("here", StringComparison.OrdinalIgnoreCase))
            {
                mentionPrefix = "@here ";
                allowedMentions.parse.Add("everyone");
            }
            else
            {
                var roleId = value.Trim('<', '>', '@', '&', ' ');
                mentionPrefix = $"<@&{roleId}> ";
                allowedMentions.roles.Add(roleId);
            }
        }

        /// <summary>
        /// Sends a detection as a rich embed (resolves the Steam avatar first for the thumbnail).
        /// </summary>
        internal static async void SendDetection(DetectionReport report)
        {
            if (webClient == null)
            {
                return; // webhook not configured
            }

            string avatarUrl = await GetSteamAvatarAsync(report.SteamId64);

            var embed = new Embed
            {
                author = new EmbedAuthor { name = report.ServerName },
                color = EmbedColorRed,
                timestamp = DateTime.UtcNow.ToString("o"),
                footer = new EmbedFooter { text = string.Empty }, // Discord shows date/time next to it
                fields =
                [
                    new EmbedField { name = "Player", value = $"[{report.PlayerName}]({report.ProfileUrl})", inlineField = true },
                    new EmbedField { name = "SteamID64", value = $"`{report.SteamId64}`", inlineField = true },
                    new EmbedField { name = "Detection", value = $"`{report.Detection}`", inlineField = false },
                    new EmbedField { name = "Evidence", value = $"```{Truncate(report.Evidence, 1000)}```", inlineField = false },
                    new EmbedField { name = "Punishment", value = report.Punishment, inlineField = false },
                    new EmbedField { name = "Map", value = $"`{report.Map}`", inlineField = true },
                    new EmbedField { name = "Address", value = $"`{report.Address}`", inlineField = true }
                ]
            };

            if (string.IsNullOrEmpty(avatarUrl) == false)
            {
                embed.thumbnail = new EmbedThumbnail { url = avatarUrl };
            }

            var payload = new WebhookData
            {
                username = "CS2AC",
                content = string.IsNullOrEmpty(mentionPrefix) ? null : mentionPrefix.TrimEnd(),
                allowed_mentions = allowedMentions,
                embeds = [embed]
            };

            await PostAsync(payload);
        }

        private static async Task PostAsync(WebhookData payload)
        {
            string json;
            try
            {
                json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });
            }
            catch (Exception e)
            {
                Globals.Log($"Unable to send Webhook (Serialize Exception | {e.Message})");
                return;
            }

            try
            {
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                var response = await webClient.PostAsync(webClientAddress, content);
                if (response.StatusCode != System.Net.HttpStatusCode.OK &&
                    response.StatusCode != System.Net.HttpStatusCode.NoContent)
                {
                    Globals.Log($"Discord webhook returned {response.StatusCode}");
                }
            }
            catch (Exception e)
            {
                Globals.Log($"Unable to send Webhook (Request Exception | {e.Message})");
            }
        }

        /// <summary>
        /// Resolves a player's full Steam avatar from their public profile XML. No API key needed.
        /// Returns "" on any failure (the embed then shows no thumbnail).
        /// </summary>
        private static async Task<string> GetSteamAvatarAsync(string steamId64)
        {
            if (string.IsNullOrEmpty(steamId64) || steamId64 == "0")
            {
                return string.Empty;
            }

            try
            {
                var xml = await webClient.GetStringAsync($"https://steamcommunity.com/profiles/{steamId64}/?xml=1");
                const string open = "<avatarFull><![CDATA[";
                const string close = "]]></avatarFull>";
                var start = xml.IndexOf(open, StringComparison.Ordinal);
                if (start < 0) return string.Empty;
                start += open.Length;
                var end = xml.IndexOf(close, start, StringComparison.Ordinal);
                return end < 0 ? string.Empty : xml.Substring(start, end - start);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string Truncate(string value, int max)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Length <= max ? value : value[..max];
        }
    }
}
