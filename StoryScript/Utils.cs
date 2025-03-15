using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MookStoryScript
{
    public static class Utils
    {
        /// <summary>
        /// 遍历指定目录下的所有文件（包括子目录）
        /// </summary>
        /// <param name="directoryPath">要遍历的目录路径</param>
        /// <param name="searchOption">搜索选项，是否包含子目录</param>
        /// <returns>文件路径的枚举器</returns>
        public static IEnumerable<string> GetFiles(string directoryPath, SearchOption searchOption = SearchOption.AllDirectories)
        {
            return Directory.EnumerateFiles(directoryPath, "*.*", searchOption);
        }

        /// <summary>
        /// 遍历指定目录下的指定扩展名的文件（包括子目录）
        /// </summary>
        /// <param name="directoryPath">要遍历的目录路径</param>
        /// <param name="extensions">文件扩展名数组（例如：new[] {".txt", ".doc"}）</param>
        /// <param name="searchOption">搜索选项，是否包含子目录</param>
        /// <returns>符合扩展名条件的文件路径的枚举器</returns>
        public static IEnumerable<string> GetFiles(string directoryPath, string[] extensions, SearchOption searchOption = SearchOption.AllDirectories)
        {
            if (extensions.Length == 0)
            {
                return GetFiles(directoryPath, searchOption);
            }

            return Directory.EnumerateFiles(directoryPath, "*.*", searchOption)
                .Where(file => extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 同步读取文件的所有内容
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>文件内容的字符串</returns>
        public static string ReadFile(string filePath)
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096);
            using var reader = new StreamReader(fileStream);
            return reader.ReadToEnd();
        }

        /// <summary>
        /// 异步读取文件的所有内容
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>文件内容的字符串</returns>
        public static async Task<string> ReadFileAsync(string filePath)
        {
            await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            using var reader = new StreamReader(fileStream);
            return await reader.ReadToEndAsync();
        }

        /// <summary>
        /// 同步写入内容到文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="content">要写入的内容</param>
        /// <param name="append">是否追加到文件末尾</param>
        public static void WriteFile(string filePath, string content, bool append = false)
        {
            using var fileStream = new FileStream(filePath, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, 4096);
            using var writer = new StreamWriter(fileStream);
            writer.Write(content);
        }

        /// <summary>
        /// 异步写入内容到文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="content">要写入的内容</param>
        /// <param name="append">是否追加到文件末尾</param>
        public static async Task WriteFileAsync(string filePath, string content, bool append = false)
        {
            await using var fileStream = new FileStream(filePath, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
            await using var writer = new StreamWriter(fileStream);
            await writer.WriteAsync(content);
        }

        /// <summary>
        /// 按行读取文件内容
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>文件行内容的枚举器</returns>
        public static IEnumerable<string> ReadLines(string filePath)
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096);
            using var reader = new StreamReader(fileStream);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                yield return line;
            }
        }

    }
}
