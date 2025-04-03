namespace CICD;

public static partial class Utilities
{
    public static string AddContentToSection(string source, string itemStart, string itemEnd, List<string> contentToAdd)
    {
        string existing = GetTextBetweenItems(source, itemStart, itemEnd);
        //string replacement = existing + contentToAdd + Environment.NewLine + new String(' ', GetIndentSpaces(existing));

        string replacement = existing;

        if (!String.IsNullOrEmpty(existing) && contentToAdd.Count() > 1) {
            replacement += Environment.NewLine + new String(' ', GetIndentSpaces(existing));
        }

        foreach (var item in contentToAdd) {
            replacement += item + Environment.NewLine + new String(' ', GetIndentSpaces(existing));
        }

        string output = ReplaceTextBetweenItems(source, itemStart, itemEnd, replacement);
        return output;
    }

    /// <summary>
    /// Takes an optional datetime value and corrects for a timezone offset.
    /// </summary>
    /// <param name="value">The nullable DateTime object.</param>
    /// <param name="TimeZoneOffsetHours">The offset values as a decimal.</param>
    /// <param name="DefaultIfNoValue">An optional value to use if the value is null.</param>
    /// <returns>A nullable DateTime object.</returns>
    public static DateTime? AdjustDateTime(DateTime? value, decimal TimeZoneOffsetHours, DateTime? DefaultIfNoValue)
    {
        DateTime? output = (DateTime?)null;
        if (value.HasValue) {
            DateTime givenValue = Convert.ToDateTime(value);
            if (TimeZoneOffsetHours != 0) {
                int Minutes = Convert.ToInt32(60 * TimeZoneOffsetHours);
                if (Minutes != 0) {
                    givenValue = givenValue.AddMinutes(Minutes);
                }
            }
            output = givenValue;
        }
        if (!output.HasValue) {
            if (DefaultIfNoValue.HasValue) {
                output = (DateTime)DefaultIfNoValue;
            } else {
                output = DateTime.Now;
            }

        }
        return output;
    }

    public static string CamelCase(string? input)
    {
        string output = !String.IsNullOrWhiteSpace(input) ? input : String.Empty;

        if (!String.IsNullOrEmpty(input)) {
            output = input.Substring(0, 1).ToLower() + input.Substring(1);
        }

        return output;
    }

    public static List<string>? ConcatenateListsOfStrings(List<string>? messages, List<string>? newMessages)
    {
        List<string>? output = messages;

        if (newMessages != null && newMessages.Count() > 0) {
            if (output == null) {
                output = new List<string>();
            }
            foreach (var msg in newMessages) {
                output.Add(msg);
            }
        }

        return output;
    }

    //// <summary>
    ///// Reads a cookie value.
    ///// </summary>
    ///// <param name="cookieName">The name of the cookie.</param>
    ///// <returns>The cookie value as a string.</returns>
    //public static string CookieRead(string cookieName, Microsoft.AspNetCore.Http.HttpContext httpContext)
    //{
    //    string output = String.Empty;

    //    if (httpContext != null) {
    //        if (!String.IsNullOrWhiteSpace(cookieName)) {
    //            try {
    //                string ck = httpContext.Request.Cookies[cookieName];
    //                if (!String.IsNullOrWhiteSpace(ck)) {
    //                    output = ck;
    //                }
    //            } catch (Exception ex) {
    //                if (ex != null) { }
    //            }
    //        }
    //        if (output.ToLower() == "cleared") { output = String.Empty; }
    //    }

    //    return System.Web.HttpUtility.HtmlDecode(output);
    //}

    ///// <summary>
    ///// Writes a cookie.
    ///// </summary>
    ///// <param name="cookieName">The name of the cookie.</param>
    ///// <param name="value">The value for the cookie.</param>
    ///// <param name="httpContext">The current HTTP Context</param>
    ///// <param name="cookieDomain">An optional domain to set for the cookie. Never used when running on localhost.</param>
    //public static void CookieWrite(string cookieName, string value, Microsoft.AspNetCore.Http.HttpContext httpContext, string cookieDomain = "")
    //{
    //    if (httpContext != null) {
    //        DateTime now = DateTime.Now;
    //        if (String.IsNullOrEmpty(cookieName)) { return; }

    //        Microsoft.AspNetCore.Http.CookieOptions option = new Microsoft.AspNetCore.Http.CookieOptions();
    //        option.Expires = now.AddYears(1);

    //        string fullUrl = GetFullUrl(httpContext);

    //        if (!String.IsNullOrWhiteSpace(cookieDomain) && !String.IsNullOrWhiteSpace(fullUrl) && !fullUrl.ToLower().Contains("localhost")) {
    //            option.Domain = cookieDomain;
    //        }

    //        httpContext.Response.Cookies.Append(cookieName, value, option);
    //    }
    //}

    public static HttpClient GetHttpClient(string url)
    {
        if (!String.IsNullOrWhiteSpace(url) && url.ToLower().Contains("//localhost")) {
            var httpClientHandler = new HttpClientHandler();
            httpClientHandler.ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) => { return true; };
            return new HttpClient(httpClientHandler);
        } else {
            return new HttpClient();
        }
    }

    /// <summary>
    /// Gets a unique filename based on the date in the yyyy.MM.dd.HH.mm.ss.ff format.
    /// </summary>
    /// <returns>A string used for a unique filename.</returns>
    public static string FileDate()
    {
        DateTime dt = new DateTime(DateTime.Now.Ticks);
        return string.Format("{0:yyyy.MM.dd.HH.mm.ss.ff}", dt);
    }

    public static string FormatAsDate(this string DateString)
    {
        string output = DateString;
        if (!String.IsNullOrWhiteSpace(output)) {
            try {
                DateTime dateOut;
                DateTime.TryParse(output, out dateOut);
                output = string.Format("{0:M/d/yyyy}", dateOut);
            } catch { }
        }
        return output;
    }

    public static string FormatAsTime(this string DateString)
    {
        string output = DateString;
        if (!String.IsNullOrWhiteSpace(output)) {
            try {
                DateTime dateOut;
                DateTime.TryParse(output, out dateOut);
                output = string.Format("{0:h:mmtt}", dateOut).ToLower();
            } catch { }
        }
        return output;
    }

    /// <summary>
    /// Takes a nullable DateTime and returns a formatted date in the shortdatestring and shorttimestring format.
    /// </summary>
    /// <param name="value">A nullable DateTime object.</param>
    /// <returns>A formatted string if a valid date was received.</returns>
    public static string FormatDateTime(DateTime? value)
    {
        string output = String.Empty;
        if (value.HasValue) {
            output = Convert.ToDateTime(value).ToShortDateString() + " " + Convert.ToDateTime(value).ToShortTimeString();
        }
        return output;
    }

    /// <summary>
    /// Converts a Guid to a binary string.
    /// </summary>
    /// <param name="guid">The Guid to convert.</param>
    /// <returns>A Guid converted to a byte array represented as a string.</returns>
    public static string GetBinaryStringFromGuid(Guid guid)
    {
        StringBuilder sb = new StringBuilder();
        foreach (byte b in guid.ToByteArray()) {
            sb.Append(string.Format(@"\{0}", b.ToString("X")));
        }
        return sb.ToString();
    }

    /// <summary>
	/// Gets the full URL of the current page.
	/// </summary>
	/// <returns>The full URL of the current page with any querystring values as well.</returns>
	//public static string GetFullUrl(Microsoft.AspNetCore.Http.HttpContext httpContext)
 //   {
 //       string output = "";

 //       if (httpContext != null) {
 //           try {
 //               output = string.Concat(
 //                   httpContext.Request.Scheme,
 //                   "://",
 //                   httpContext.Request.Host.ToUriComponent(),
 //                   httpContext.Request.PathBase.ToUriComponent(),
 //                   httpContext.Request.Path.ToUriComponent(),
 //                   httpContext.Request.QueryString.ToUriComponent()
 //               );
 //           } catch { }
 //       }

 //       return output;
 //   }


    public static int GetIndentSpaces(string source)
    {
        int output = 0;

        if (!String.IsNullOrEmpty(source)) {
            string[] lines = source.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
            if (lines != null && lines.Any()) {
                foreach (var line in lines) {
                    if (output == 0 && line.StartsWith(" ")) {
                        int indent = line.TakeWhile(Char.IsWhiteSpace).Count();
                        if (indent > 2) {
                            output = indent;
                        }
                    }
                }
            }
        }

        return output;
    }

    /// <summary>
    /// Gets the mimetype for a given file extension.
    /// </summary>
    /// <param name="extension">A file extension, either with or without the starting period.</param>
    /// <returns>The mimetype if known, otherwise returns application/octet-stream.</returns>
    public static string GetMimeType(string extension)
    {
        if (extension == null) {
            throw new ArgumentNullException("extension");
        }

        if (!extension.StartsWith(".")) {
            extension = "." + extension;
        }

        string? mime;

        var output = _mappings.TryGetValue(extension, out mime) ? mime : "application/octet-stream";
        return String.Empty + output;
    }

    public static string GetTextBetweenItems(string source, string itemStart, string itemEnd)
    {
        string output = String.Empty;

        int start = source.ToLower().IndexOf(itemStart.ToLower());

        if (start > -1) {
            int end = source.ToLower().IndexOf(itemEnd.ToLower());
            if (end > -1) {
                output = source.Substring(start + itemStart.Length, end - (start + itemStart.Length));
            }
        }

        return output;
    }

    public static string HtmlDecode(this string HtmlToDecode)
    {
        return System.Web.HttpUtility.HtmlDecode(HtmlToDecode);
    }

    public static string HtmlEncode(this string HtmlToEncode)
    {
        return System.Web.HttpUtility.HtmlEncode(HtmlToEncode);
    }

    public static bool IsDate(this string Input)
    {
        bool output = false;
        DateTime time1;
        try {
            output = DateTime.TryParse((string)Input, out time1);
        } catch { }
        return output;
    }

    public static bool IsEmailAddress(this string EmailAddress)
    {
        bool output = false;
        try {
            var addr = new System.Net.Mail.MailAddress(EmailAddress);
            output = addr.Address == EmailAddress;
            if (output == true) {
                output = addr.Host.Contains(".");
            }
        } catch { }
        return output;
    }

    public static bool IsGuid(this string Input)
    {
        bool output = false;
        if (!String.IsNullOrWhiteSpace(Input)) {
            Guid test = Guid.Empty;
            try {
                test = new Guid(Input);
                if (test != Guid.Empty) { output = true; }
            } catch { }
        }
        return output;
    }

    public static bool IsNumeric(this string Input)
    {
        bool output = false;
        if (!String.IsNullOrWhiteSpace(Input)) {
            try {
                decimal decTest = Convert.ToDecimal(Input);
                output = true;
            } catch { }
            if (!output) {
                try {
                    double longTest = Convert.ToInt64(Input);
                    output = true;
                } catch { }
            }
        }
        return output;
    }

    /// <summary>
    /// Finds only the numbers in a given string.
    /// </summary>
    /// <param name="Input">The string input.</param>
    /// <returns>Any numbers in the string.</returns>
    public static string NumbersOnly(string Input)
    {
        string output = String.Empty;
        try {
            Regex regexObj = new Regex(@"[^\d]");
            string resultString = regexObj.Replace(Input, "");
            if (!String.IsNullOrWhiteSpace(resultString)) {
                output = resultString;
            }
        } catch { }
        return output;
    }

    public static string ReplaceInString(this string originalString, string oldValue, string newValue, bool CaseSensitive = false)
    {
        int startIndex = 0;
        string replaceString = originalString + String.Empty;
        while (true) {
            if (CaseSensitive) {
                startIndex = replaceString.IndexOf(oldValue, startIndex, System.StringComparison.InvariantCulture);
            } else {
                startIndex = replaceString.IndexOf(oldValue, startIndex, System.StringComparison.InvariantCultureIgnoreCase);
            }
            if (startIndex == -1) { break; }
            replaceString = replaceString.Substring(0, startIndex) + newValue + replaceString.Substring(startIndex + oldValue.Length);
            startIndex += newValue.Length;
        }
        return replaceString;
    }

    public static string ReplaceTextBetweenItems(string source, string itemStart, string itemEnd, string replacement)
    {
        string output = source;

        int start = source.ToLower().IndexOf(itemStart.ToLower());

        if (start > -1) {
            int end = source.ToLower().IndexOf(itemEnd.ToLower());
            if (end > -1) {
                string before = source.Substring(0, start + itemStart.Length);
                string after = source.Substring(end);

                output = before + replacement + after;
            }
        }

        return output;
    }

    public static string SerializeObjectToJsonCamelCase(object? o)
    {
        string output = "{}";

        if (o != null) {
            var serialized = System.Text.Json.JsonSerializer.Serialize(o, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
            if (!String.IsNullOrWhiteSpace(serialized)) {
                output = serialized;
            }
        }

        return output;
    }

    public static string SerializeObjectToJsonPascalCase(object o)
    {
        string output = "{}";

        if (o != null) {
            var serialized = System.Text.Json.JsonSerializer.Serialize(o);
            if (!String.IsNullOrWhiteSpace(serialized)) {
                output = serialized;
            }
        }

        return output;
    }

    /// <summary>
    /// Generates a unique string based on the UNIX epoch date.
    /// </summary>
    /// <returns>A unique string.</returns>
    public static string UniqueString()
    {
        return DateTime.Now.Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds.ToString("0");
    }
}
