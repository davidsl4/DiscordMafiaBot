using Discord;

namespace MafiaDiscordBot.Extensions
{
    public static class Discord_EmbedBuilderExtensions
    {
        /// <summary>
        ///     Adds an inline <see cref="T:Discord.Embed" /> field with the provided name and value.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="name">The title of the field.</param>
        /// <param name="value">The value of the field.</param>
        /// <returns>The current builder.</returns>
        public static EmbedBuilder AddInlineField(this EmbedBuilder builder, string name, string value) =>
            builder.AddField(name, value, true);
    }
}