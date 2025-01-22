using System;
using System.Collections.Generic;
using System.IO;

namespace CodeTranslation
{
    class Program
    {
        static List<(string FileName, byte[] Content, int Count)> ExtractFilesWithConditions(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path), "路径参数不能为null");
            }
            var extractedFiles = new List<(string FileName, byte[] Content, int Count)>();

            foreach (var filePath in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                var fileName = Path.GetFileName(filePath);
                byte[] content;
                try
                {
                    content = File.ReadAllBytes(filePath);
                }
                catch (IOException e)
                {
                    Console.WriteLine($"读取文件 {filePath} 时出错: {e.Message}");
                    continue;
                }

                int index = 0;
                int? currentHeaderStart = null;
                int fileCount = 1;

                while (index < content.Length)
                {
                    int headerStartIndex = FindBytes(content, index, new byte[] { 0x80, 0x00 });
                    if (headerStartIndex == -1)
                    {
                        if (currentHeaderStart.HasValue)
                        {
                            var searchRange = new byte[content.Length - currentHeaderStart.Value];
                            Array.Copy(content, currentHeaderStart.Value, searchRange, 0, searchRange.Length);
                            extractedFiles.Add((fileName, searchRange, fileCount));
                            fileCount++;
                        }
                        break;
                    }

                    int checkLength = Math.Min(10, content.Length - headerStartIndex);
                    var checkSegment = new byte[checkLength];
                    Array.Copy(content, headerStartIndex, checkSegment, 0, checkLength);

                    if (ContainsBytes(checkSegment, new byte[] { 0x03, 0x12, 0x04, 0x01, 0x00, 0x00 }) ||
                        ContainsBytes(checkSegment, new byte[] { 0x03, 0x12, 0x04, 0x02, 0x00, 0x00 }))
                    {
                        int nextHeaderIndex = FindBytes(content, headerStartIndex + 1, new byte[] { 0x80, 0x00 });
                        if (!currentHeaderStart.HasValue)
                        {
                            currentHeaderStart = headerStartIndex;
                        }
                        else
                        {
                            var rangeLength = headerStartIndex - currentHeaderStart.Value;
                            var searchRange = new byte[rangeLength];
                            Array.Copy(content, currentHeaderStart.Value, searchRange, 0, rangeLength);

                            if (ContainsBytes(searchRange, new byte[] { 0x28, 0x63, 0x29, 0x43, 0x52, 0x49 }))
                            {
                                extractedFiles.Add((fileName, searchRange, fileCount));
                                fileCount++;
                            }
                            currentHeaderStart = headerStartIndex;
                        }
                    }

                    index = headerStartIndex + 1;
                }
            }

            return extractedFiles;
        }

        static int[] ComputeLPSArray(byte[] pattern)
        {
            int[] lps = new int[pattern.Length];
            int len = 0;
            lps[0] = 0;

            int i = 1;
            while (i < pattern.Length)
            {
                if (pattern[i] == pattern[len])
                {
                    len++;
                    lps[i] = len;
                    i++;
                }
                else
                {
                    if (len != 0)
                    {
                        len = lps[len - 1];
                    }
                    else
                    {
                        lps[i] = 0;
                        i++;
                    }
                }
            }
            return lps;
        }

        static int FindBytes(byte[] data, int startIndex, byte[] pattern)
        {
            int[] lps = ComputeLPSArray(pattern);
            int i = startIndex;
            int j = 0;

            while (i < data.Length)
            {
                if (pattern[j] == data[i])
                {
                    i++;
                    j++;
                }

                if (j == pattern.Length)
                {
                    return i - j;
                }
                else if (i < data.Length && pattern[j] != data[i])
                {
                    if (j != 0)
                    {
                        j = lps[j - 1];
                    }
                    else
                    {
                        i++;
                    }
                }
            }
            return -1;
        }

        static bool ContainsBytes(byte[] data, byte[] pattern)
        {
            return FindBytes(data, 0, pattern) != -1;
        }

        static void Main(string[] args)
        {
            Console.Write("请输入要遍历的文件夹路径: ");
            string? inputPath = Console.ReadLine();
            if (inputPath == null)
            {
                Console.WriteLine("输入的路径不能为null，请重新输入。");
                return;
            }

            try
            {
                var extractedFiles = ExtractFilesWithConditions(inputPath);

                foreach (var (fileName, fileContent, count) in extractedFiles)
                {
                    string? directory = Path.GetDirectoryName(inputPath);
                    if (directory == null)
                    {
                        Console.WriteLine($"获取目录失败，输入路径: {inputPath}");
                        continue;
                    }
                    string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
                    string outputFilePath;
                    if (count == 1)
                    {
                        outputFilePath = Path.Combine(directory, $"{fileNameWithoutExtension}.adx");
                    }
                    else
                    {
                        outputFilePath = Path.Combine(directory, $"{fileNameWithoutExtension}_{count}.adx");
                    }
                    File.WriteAllBytes(outputFilePath, fileContent);
                    Console.WriteLine($"已提取文件: {outputFilePath}");
                }

                Console.WriteLine($"共提取出 {extractedFiles.Count} 个符合条件的文件片段。");
            }
            catch (Exception e)
            {
                Console.WriteLine($"操作过程中出现错误: {e.Message}");
            }
        }
    }
}