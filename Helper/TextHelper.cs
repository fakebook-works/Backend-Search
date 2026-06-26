using System.Globalization;
using System.Text;

namespace Backend_Search_Fakebook.Helper
{
    // Hàm Tokenize: tách chuỗi các từ 
    public static class TextHelper 
    {
        public static List<string> Tokenize (string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new List<string>(); // Trả về mảng rỗng nếu đầu vào trống
            }
            // 1. Chuyển toàn bộ thành chữ thường (Lowercase)
            string lowerText = text.ToLower().Trim();

            // 2. Loại bỏ dấu tiếng Việt
            string noAccentText = RemoveDiacritics(lowerText);

            // 3. Tách chuỗi thành mảng dựa trên khoảng trắng
            // StringSplitOptions.RemoveEmptyEntries giúp tự động vứt bỏ các khoảng trắng thừa
            string[] tokensArray = noAccentText.Split(
                new char[] { ' ', '\t', '\n', '\r' },
                StringSplitOptions.RemoveEmptyEntries
            );

            // Chuyển mảng (Array) thành List và trả về
            return tokensArray.ToList();
        }

        // Hàm: Thuật toán bóc tách dấu tiếng Việt bằng Unicode
        private static string RemoveDiacritics(string text)
        {
            // Xử lý riêng chữ 'đ' vì bộ mã Unicode chuẩn không coi 'đ' là 'd' có dấu
            text = text.Replace("đ", "d").Replace("Đ", "d");

            // FormD bóc tách chữ "ễ" thành 3 phần: "e" + "^" + "~"
            var normalizedString = text.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (var c in normalizedString)
            {
                // Kiểm tra xem ký tự hiện tại có phải là dấu câu (NonSpacingMark) hay không
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    // Nếu không phải là dấu thì mới giữ lại (giữ chữ "e", vứt "^" và "~")
                    stringBuilder.Append(c);
                }
            }

            // FormC ghép các ký tự rời rạc lại thành chuỗi hoàn chỉnh
            return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        }
    }
 }

