using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;

namespace Lock.Models
{
    [Table("CoinTransactions")]
    public class CoinTransaction : BaseModel
    {
        [PrimaryKey("id")]
        public int Id { get; set; }

        [Column("user_phone")]
        public string UserPhone { get; set; } = string.Empty;

        [Column("amount")]
        public int Amount { get; set; }

        [Column("type")]
        public string Type { get; set; } = string.Empty;

        [Column("reference")]
        public string Reference { get; set; } = string.Empty;

        [Column("description")]
        public string Description { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}