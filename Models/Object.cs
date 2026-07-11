using System.Collections.Generic;

namespace BackEndSearchFakebook.Models
{
    public partial class Object
    {
        // Nhận ID từ bên ngoài (Database gốc sinh ra, không dùng Identity tự tăng)
        public long Id { get; set; }
        public short Type { get; set; } 
        public int? SortKey { get; set; }

        // Đã xóa OwnerId và PrivacyLevel theo chuẩn Microservices

        // Quan hệ Nhiều - Nhiều với Token
        public virtual ICollection<Token> Tokens { get; set; } = new List<Token>();
    }
}