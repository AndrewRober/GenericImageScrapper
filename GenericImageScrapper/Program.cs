using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SixLabors.ImageSharp;

namespace GenericImageScrapper
{
    class Program
    {
        private static ConcurrentBag<string> VisitedLinks = new ConcurrentBag<string>();
        private static ConcurrentBag<Uri> DownloadedImages = new ConcurrentBag<Uri>();
        private static ConcurrentStack<string> links = new ConcurrentStack<string>();
        static void Main(string[] args)
        {
            args = new[] { "" };
            //check if there is an argument passed to the app then check if it's a valid url
            if (!string.IsNullOrEmpty(args?[0]) && Uri.TryCreate(args[0], UriKind.Absolute, out var url))
            {
                using (var client = new WebClient())
                {
                    AddHeadersToWebClient(client);
                    var html = Regex.Replace(client.DownloadString(url), @"\s+", "");
                    var dir = Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(),
                        DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss"))).FullName;
                    links.PushRange(Regex.Matches(html,
                            "(http|ftp|https):\\/\\/([\\w_-]+(?:(?:\\.[\\w_-]+)+))([\\w.,@?^=%&:\\/~+#-]*[\\w@?^=%&\\/~+#-])?")
                        .Select(lg => lg.Groups[0].Value).Distinct().ToArray());
                    ScrapImages(html, url, dir);
                    VisitedLinks.Add(args[0]);
                    while (links.Any())
                    {
                        links.TryPop(out var link);
                        if (string.IsNullOrEmpty(link)) continue;
                        if (!link.Contains(url.Host) || VisitedLinks.Contains(link))
                            continue;
                        try
                        {
                            html = Regex.Replace(client.DownloadString(link), @"\s+", "");
                            links.PushRange(Regex.Matches(html,
                                    "(http|ftp|https):\\/\\/([\\w_-]+(?:(?:\\.[\\w_-]+)+))([\\w.,@?^=%&:\\/~+#-]*[\\w@?^=%&\\/~+#-])?")
                                .Select(lg => lg.Groups[0].Value).Distinct().Where(nl => links.All(l => l != nl)).ToArray());
                            ScrapImages(html, url, dir);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                        }
                        VisitedLinks.Add(link);
                    }
                }
            }
        }

        static void ScrapImages(string html, Uri url, string dir)
        {
            var imgs = new List<Uri>();
            imgs.AddRange(Regex.Matches(html, "<img(.*?)src=\"(.*?)\"", RegexOptions.Compiled)
                .Where(src => !src.Value.ToLower().Contains("base64"))
                .Select(src => new Uri(url, src.Groups[2].Value)));
            imgs.AddRange(Regex.Matches(html, "<img(.*?)data-src=\"(.*?)\"", RegexOptions.Compiled)
                .Where(src => !src.Value.ToLower().Contains("base64"))
                .Select(src => new Uri(url, src.Groups[2].Value)));
            imgs.AddRange(Regex.Matches(html, "(background|background-image):url\\(['\"]?(.*?)['\"]?\\)", RegexOptions.Compiled)
                .Where(src => !src.Value.ToLower().Contains("base64"))
                .Select(src => new Uri(url, src.Groups[2].Value)));
            Regex.Matches(html, "data:image\\/\\w+;base64,[^='\"&]*[=]*", RegexOptions.Compiled)
                .Select(m => m.Value).ToList().ForEach(img =>
                {
                    try
                    {
                        File.WriteAllBytes($"{Path.Combine(dir, Path.GetRandomFileName())}.{img.Split(';')[0].Split('/')[1]}",
                            Convert.FromBase64String(img.Split(',')[1]));
                    }
                    catch { Console.WriteLine($"failed to save a base64 image with the string of {img}"); }
                });
            imgs = imgs.Distinct().ToList();
            if (imgs.Any())
            {
                Parallel.ForEach(imgs, imgUri =>
                {
                    try
                    {
                        if (DownloadedImages.Any(i => i.AbsolutePath == imgUri.AbsolutePath))
                            return;

                        using (var wc = new WebClient())
                        {
                            AddHeadersToWebClient(wc);
                            wc.OpenRead(imgUri);
                            var size = Convert.ToInt64(wc.ResponseHeaders["Content-Length"]);
                            if (size < 10000) return;
                        }

                        using (var imgClient = new WebClient())
                        {

                            AddHeadersToWebClient(imgClient);
                            using (var ms = new MemoryStream(imgClient.DownloadData(imgUri)))
                            {
                                var contentType = imgClient.ResponseHeaders["Content-Type"];
                                //using (var fs = File.Create(Path.Combine(dir, HandleGettingFileName(imgClient))))
                                using (var fs = File.Create($"{dir}\\{imgUri.AbsolutePath.Substring(imgUri.AbsolutePath.LastIndexOf('/') + 1)}"))
                                {
                                    fs.Position = 0;
                                    fs.Write(ms.ToArray());
                                    fs.Flush();
                                    DownloadedImages.Add(imgUri);
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                });
            }
        }

        static void AddHeadersToWebClient(WebClient client)
        {
            client.Headers.Add("Accept", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/77.0.3865.120 Safari/537.36");
            client.Headers.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/76.0.3809.132 Safari/537.36");
            client.Headers.Add("Accept-Language", "en-US,en;q=0.9");
            client.Headers.Add("upgrade-insecure-requests", "1");
            client.Headers.Add("Cache-Control", "max-age=0");
            client.Headers.Add("sec-fetch-mode", "navigate");
            client.Headers.Add("sec-fetch-site", "none");
            client.Headers.Add("sec-fetch-user", "?1");
        }

        static string HandleGettingFileName(WebClient client)
        {
            string fileName = string.Empty;
            try
            {
                // Try to extract the filename from the Content-Disposition header
                if (!string.IsNullOrEmpty(client.ResponseHeaders["Content-Disposition"]))
                {
                    fileName = client.ResponseHeaders["Content-Disposition"]
                        .Substring(client.ResponseHeaders["Content-Disposition"].IndexOf("filename=") + 9)
                        .Replace("\"", "");
                }
            }
            catch { /* suppress the error */ }
            if (string.IsNullOrEmpty(fileName))
                fileName = Path.GetRandomFileName();
            else if (File.Exists(fileName))
                fileName = getNextFileName(fileName);

            return fileName.Contains(".jpeg") ? fileName : fileName + ".jpeg";
        }

        static string getNextFileName(string fileName)
        {
            var extension = Path.GetExtension(fileName);
            var i = 0;
            while (File.Exists(fileName))
                fileName = i == 0
                    ? fileName.Replace(extension, "(" + ++i + ")" + extension)
                    : fileName.Replace("(" + i + ")" + extension, "(" + ++i + ")" + extension);
            return fileName;
        }
    }
}
