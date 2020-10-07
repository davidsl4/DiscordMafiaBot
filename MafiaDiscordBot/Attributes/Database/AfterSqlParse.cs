using System;

namespace MafiaDiscordBot.Attributes.Database
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property)]
    internal class AfterSqlParseAttribute : Attribute
    {
        public string MethodName { get; }
        public string[] SqlColumnsAsArgs { get; }

        public AfterSqlParseAttribute(string methodName, params string[] sqlColumnsAsArgs)
        {
            MethodName = methodName;
            SqlColumnsAsArgs = sqlColumnsAsArgs;
        }
    }
}