using Discord;

namespace MafiaDiscordBot.Extensions
{
    internal static class Discord_IUserExtensions
    {
        public static string
            FixGetAvatarUrl(this IUser user, ImageFormat format = ImageFormat.Auto, ushort size = 128) => user.GetAvatarUrl(format, size) ?? user.GetDefaultAvatarUrl();
    }
}