using System;
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
        static void Main(string[] args)
        {
            //check if there is an argument passed to the app then check if it's a valid url
            if (!string.IsNullOrEmpty(args?[0]) && Uri.TryCreate(args[0], UriKind.Absolute, out var url))
            {
                using (var client = new WebClient())
                {
                    var html = Regex.Replace(client.DownloadString(url), @"\s+", "");
                    var dir = Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(),
                        DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss"))).FullName;
                    var imgs = new List<Uri>();
                    imgs.AddRange(Regex.Matches(html, "<img(.*?)src=\"(.*?)\"", RegexOptions.Compiled)
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
                            } catch { Console.WriteLine($"failed to save a base64 image with the string of {img}"); }
                        });
                    if (imgs.Any())
                    {
                        Parallel.ForEach(imgs, imgUri =>
                        {
                            try
                            {
                                using (var imgClient = new WebClient())
                                using (var ms = new MemoryStream(imgClient.DownloadData(imgUri)))
                                using (var img = Image.Load(ms))
                                using (var fs = File.Create(Path.Combine(dir, HandleGettingFileName(imgClient))))
                                {
                                    img.SaveAsJpeg(fs);
                                }
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e);
                            }
                        });
                    }
                }
            }
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
