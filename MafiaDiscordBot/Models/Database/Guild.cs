using System;
using System.Diagnostics.CodeAnalysis;
using SqlKata;

namespace MafiaDiscordBot.Models.Database
{
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
    public class Guild : IDatabaseObject
    {
        private ulong _id;
        private ulong _gameCategoryId;
        private string _prefix;
        private string _localization;

        [Column("id")]
        public ulong ID
        {
            get
            {
                if (Filled) LastAccessed = DateTime.Now;
                return _id;
            }
            set
            {
                _id = value;
                if (Filled)
                    LastModified = LastAccessed = DateTime.Now;
            }
        }

        [Column("game_category")]
        public ulong GameCategoryId
        {
            get
            {
                if (Filled) LastAccessed = DateTime.Now;
                return _gameCategoryId;
            }
            set
            {
                _gameCategoryId = value;
                if (Filled)
                    LastModified = LastAccessed = DateTime.Now;
            }
        }

        [Column("prefix")]
        public string Prefix
        {
            get
            {
                if (Filled) LastAccessed = DateTime.Now;
                return _prefix;
            }
            set
            {
                _prefix = value;
                if (Filled)
                    LastModified = LastAccessed = DateTime.Now;
            }
        }

        [Column("localization")]
        public string Localization
        {
            get
            {
                if (Filled) LastAccessed = DateTime.Now;
                return _localization;
            }
            set
            {
                _localization = value;
                if (Filled)
                    LastModified = LastAccessed = DateTime.Now;
            }
        }

        public DateTime LastAccessed { get; set; }
        public DateTime? LastModified { get; set; }
        public bool Filled { get; set; }
    }
}