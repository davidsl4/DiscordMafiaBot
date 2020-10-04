using System;

namespace MafiaDiscordBot.Attributes.Database
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property, AllowMultiple = false)]
    class AfterSqlParseAttribute : Attribute
    {
        private string _methodName;
        private string[] _sqlColumnsAsArgs;

        public string MethodName { get => _methodName; }
        public string[] SqlColumnsAsArgs { get => _sqlColumnsAsArgs; }

        public AfterSqlParseAttribute(string methodName, params string[] sqlColumnsAsArgs)
        {
            _methodName = methodName;
            _sqlColumnsAsArgs = sqlColumnsAsArgs;
        }
    }
}