﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Utils.cs" company="">
//   
// </copyright>
// <summary>
//   The utils.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace BackupManager
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text.RegularExpressions;
    using System.Runtime.InteropServices;

    using PushbulletSharp;
    using PushbulletSharp.Models.Requests;
    using PushbulletSharp.Models.Responses;

    /// <summary>
    /// The utils.
    /// </summary>
    public class Utils
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetDiskFreeSpaceEx(string lpDirectoryName,
        out ulong lpFreeBytesAvailable,
        out ulong lpTotalNumberOfBytes,
        out ulong lpTotalNumberOfFreeBytes);

        #region Constants

        /// <summary>
        ///     The end block size.
        /// </summary>
        private const int EndBlockSize = 16 * 1024; // 16K

        /// <summary>
        ///     The middle block size.
        /// </summary>
        private const int MiddleBlockSize = 16 * 1024; // 16K

        /// <summary>
        ///     The start block size.
        /// </summary>
        private const int StartBlockSize = 16 * 1024; // 16K

        #endregion

        #region Static Fields

        /// <summary>
        ///     This is the Hash for a file containing 48K of only zero bytes.
        /// </summary>
        public static string ZeroByteHash = "f4f35d60b3cc18aaa6d8d92f0cd3708a";

        /// <summary>
        /// The md 5.
        /// </summary>
        private static readonly MD5CryptoServiceProvider Md5 = new MD5CryptoServiceProvider();

        #endregion

        #region Public Methods and Operators

        /// <summary>
        /// Returns True if any of the attrbutes to check for are set in the value.
        /// </summary>
        /// <param name="value">
        /// </param>
        /// <param name="flagsToCheckFor">
        /// </param>
        /// <returns>
        /// The <see cref="bool"/>.
        /// </returns>
        public static bool AnyFlagSet(FileAttributes value, FileAttributes flagsToCheckFor)
        {
            if (flagsToCheckFor == 0)
            {
                return false;
            }

            return Enum.GetValues(typeof(FileAttributes)).Cast<Enum>().Where(flagsToCheckFor.HasFlag).Any(value.HasFlag);
        }

        /// <summary>
        /// Clears the attribute from the file if it were set.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="attributeToRemove"></param>
        public static void ClearFileAttribute(string path, FileAttributes attributeToRemove)
        {
            FileAttributes attributes = File.GetAttributes(path);

            if ((attributes & attributeToRemove) == attributeToRemove)
            {
                attributes = RemoveAttribute(attributes, attributeToRemove);
                File.SetAttributes(path, attributes);
            }
        }

        private static FileAttributes RemoveAttribute(FileAttributes attributes, FileAttributes attributesToRemove)
        {
            return attributes & ~attributesToRemove;
        }

        /// <summary>
        /// Creates a hash for the 3 byte arrays passed in.
        /// </summary>
        /// <param name="firstByteArray">
        /// The start byte array.
        /// </param>
        /// <param name="secondByteArray">
        /// The middle byte array.
        /// </param>
        /// <param name="thirdByteArray">
        /// The end byte array.
        /// </param>
        /// <returns>
        /// A String of the hash.
        /// </returns>
        public static string CreateHashForByteArray(
            byte[] firstByteArray, 
            byte[] secondByteArray, 
            byte[] thirdByteArray)
        {
            byte[] byteArrayToHash;

            if (secondByteArray == null && thirdByteArray == null)
            {
                byteArrayToHash = new byte[firstByteArray.Length];
            }
            else if (thirdByteArray == null)
            {
                byteArrayToHash = new byte[firstByteArray.Length + secondByteArray.Length];
            }
            else
            {
                byteArrayToHash = new byte[firstByteArray.Length + secondByteArray.Length + thirdByteArray.Length];
            }

            Buffer.BlockCopy(firstByteArray, 0, byteArrayToHash, 0, firstByteArray.Length);

            if (secondByteArray != null)
            {
                Buffer.BlockCopy(secondByteArray, 0, byteArrayToHash, secondByteArray.Length, secondByteArray.Length);
            }

            if (thirdByteArray != null)
            {
                Buffer.BlockCopy(
                    thirdByteArray, 
                    0, 
                    byteArrayToHash, 
                    firstByteArray.Length + secondByteArray.Length, 
                    thirdByteArray.Length);
            }

            return ByteArrayToString(Md5.ComputeHash(byteArrayToHash));
        }

        /// <summary>
        /// Ensures all the folders on the way to the file are created.
        /// </summary>
        /// <param name="filePath">
        /// </param>
        public static void EnsureDirectories(string filePath)
        {
            Directory.CreateDirectory(new FileInfo(filePath).DirectoryName);
        }

        /// <summary>
        /// Returns the drive free space in GB for the drive this file is on.
        /// </summary>
        /// <param name="path">
        /// </param>
        /// <returns>
        /// Freespace in GB
        /// </returns>
        public static long GetDiskFreeSpace(string path)
        {
            long availableSpace;

            var result = Utils.GetDiskFreeSpace(path, out availableSpace);

            if (result)
            {
                return availableSpace / (1024 * 1024 * 1024);
            }
            else
            {
                return 0;
            }
        }

    //{
    //        string driveName = Path.GetPathRoot(path);

    //        if (driveName == null)
    //        {
    //            return -1;
    //        }

    //        string actualDriveName =
    //            EnsurePathHasATerminatingSeparator(Environment.ExpandEnvironmentVariables(driveName));

    //        foreach (DriveInfo drive in DriveInfo.GetDrives())
    //        {
    //            if (drive.IsReady
    //                && drive.RootDirectory.Name.Equals(actualDriveName, StringComparison.InvariantCultureIgnoreCase))
    //            {
    //                return drive.AvailableFreeSpace / (1024 * 1024 * 1024);
    //            }
    //        }

    //        return -1;
    //    }

        /// <summary>
        /// The get files.
        /// </summary>
        /// <param name="path">
        /// The path.
        /// </param>
        /// <returns>
        /// The <see cref="string[]"/>.
        /// </returns>
        public static string[] GetFiles(string path)
        {
            return GetFiles(path, "*", SearchOption.AllDirectories, 0, 0);
        }

        /// <summary>
        /// The get files.
        /// </summary>
        /// <param name="path">
        /// The path.
        /// </param>
        /// <param name="filters">
        /// The filters.
        /// </param>
        /// <returns>
        /// The <see cref="string[]"/>.
        /// </returns>
        public static string[] GetFiles(string path, string filters)
        {
            return GetFiles(path, filters, SearchOption.AllDirectories, 0, 0);
        }

        /// <summary>
        /// The get files.
        /// </summary>
        /// <param name="path">
        /// The path.
        /// </param>
        /// <param name="filters">
        /// The filters.
        /// </param>
        /// <param name="searchOption">
        /// The search option.
        /// </param>
        /// <param name="directoryAttributesToIgnore">
        /// The directory attributes to ignore.
        /// </param>
        /// <returns>
        /// The <see cref="string[]"/>.
        /// </returns>
        public static string[] GetFiles(
            string path, 
            string filters, 
            SearchOption searchOption, 
            FileAttributes directoryAttributesToIgnore)
        {
            return GetFiles(path, filters, searchOption, directoryAttributesToIgnore, 0);
        }

        /// <summary>
        /// The get files.
        /// </summary>
        /// <param name="path">
        /// The path.
        /// </param>
        /// <param name="filters">
        /// The filters.
        /// </param>
        /// <param name="searchOption">
        /// The search option.
        /// </param>
        /// <returns>
        /// The <see cref="string[]"/>.
        /// </returns>
        public static string[] GetFiles(string path, string filters, SearchOption searchOption)
        {
            return GetFiles(path, filters, searchOption, 0, 0);
        }

        /// <summary>
        /// The get files.
        /// </summary>
        /// <param name="path">
        /// The path.
        /// </param>
        /// <param name="filters">
        /// The filters.
        /// </param>
        /// <param name="searchOption">
        /// The search option.
        /// </param>
        /// <param name="directoryAttributesToIgnore">
        /// The directory attributes to ignore.
        /// </param>
        /// <param name="fileAttributesToIgnore">
        /// The file attributes to ignore.
        /// </param>
        /// <returns>
        /// The <see cref="string[]"/>.
        /// </returns>
        public static string[] GetFiles(
            string path, 
            string filters, 
            SearchOption searchOption, 
            FileAttributes directoryAttributesToIgnore, 
            FileAttributes fileAttributesToIgnore)
        {
            if (!Directory.Exists(path))
            {
                return new string[] { };
            }

            var directoryInfo = new DirectoryInfo(path);

            if (directoryInfo.Parent != null && AnyFlagSet(directoryInfo.Attributes, directoryAttributesToIgnore))
            {
                return new string[] { };
            }

            IEnumerable<string> include =
                from filter in filters.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                where !string.IsNullOrEmpty(filter.Trim())
                select filter.Trim();

            IEnumerable<string> exclude = from filter in include where filter.Contains(@"!") select filter;

            include = include.Except(exclude);

            if (!include.Any())
            {
                include = new[] { "*" };
            }

            IEnumerable<string> excludeFilters = from filter in exclude
                                                 let replace =
                                                     filter.Replace("!", string.Empty)
                                                     .Replace(".", @"\.")
                                                     .Replace("*", ".*")
                                                     .Replace("?", ".")
                                                 select string.Format("^{0}$", replace);

            var excludeRegex = new Regex(string.Join("|", excludeFilters.ToArray()), RegexOptions.IgnoreCase);

            var pathsToSearch = new Queue<string>();
            var foundFiles = new List<string>();

            pathsToSearch.Enqueue(path);

            while (pathsToSearch.Count > 0)
            {
                string dir = pathsToSearch.Dequeue();

                if (searchOption == SearchOption.AllDirectories)
                {
                    foreach (string subDir in
                        Directory.GetDirectories(dir)
                            .Where(
                                subDir =>
                                (!AnyFlagSet(new DirectoryInfo(subDir).Attributes, directoryAttributesToIgnore))))
                    {
                        pathsToSearch.Enqueue(subDir);
                    }
                }

                foreach (string filter in include)
                {
                    string[] allfiles = Directory.GetFiles(dir, filter, SearchOption.TopDirectoryOnly);

                    IEnumerable<string> collection = exclude.Any()
                                                         ? allfiles.Where(p => !excludeRegex.Match(p).Success)
                                                         : allfiles;

                    foundFiles.AddRange(
                        collection.Where(p => (!AnyFlagSet(new FileInfo(p).Attributes, fileAttributesToIgnore))));
                }
            }

            return foundFiles.ToArray();
        }

        /// <summary>
        /// The get hash from file.
        /// </summary>
        /// <param name="fileName">
        /// The file name.
        /// </param>
        /// <param name="algorithm">
        /// The algorithm.
        /// </param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        public static string GetHashFromFile(string fileName, HashAlgorithm algorithm)
        {
            using (var stream = new BufferedStream(File.OpenRead(fileName), 1048576))
            {
                return ByteArrayToString(algorithm.ComputeHash(stream));
            }
        }

        /// <summary>
        /// The get remote file byte array.
        /// </summary>
        /// <param name="fileStream">
        /// The file stream.
        /// </param>
        /// <param name="offset">
        /// The offset.
        /// </param>
        /// <param name="byteCountToReturn">
        /// The byte count to return.
        /// </param>
        /// <returns>
        /// The <see cref="byte[]"/>.
        /// </returns>
        public static byte[] GetRemoteFileByteArray(Stream fileStream, long offset, long byteCountToReturn)
        {
            var buffer = new byte[byteCountToReturn];

            int count;
            int sum = 0;
            int length = Convert.ToInt32(byteCountToReturn);
            while ((count = fileStream.Read(buffer, sum, length - sum)) > 0)
            {
                sum += count; // sum is a buffer offset for next reading
            }

            if (sum < byteCountToReturn)
            {
                var byteArray = new byte[sum];
                Buffer.BlockCopy(buffer, 0, byteArray, 0, sum);
                return byteArray;
            }

            return buffer;
        }

        /// <summary>
        /// The get short md 5 hash from file.
        /// </summary>
        /// <param name="stream">
        /// The stream.
        /// </param>
        /// <param name="size">
        /// The size.
        /// </param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        public static string GetShortMd5HashFromFile(FileStream stream, long size)
        {
            if (stream == null)
            {
                return null;
            }

            if (size <= 0)
            {
                return string.Empty;
            }

            byte[] startBlock;
            byte[] middleBlock = null;
            byte[] endBlock = null;

            if (size >= StartBlockSize + MiddleBlockSize + EndBlockSize)
            {
                long startDownloadPositionForEndBlock = size - EndBlockSize;

                long startDownloadPositionForMiddleBlock = size / 2;

                startBlock = GetLocalFileByteArray(stream, 0, StartBlockSize);

                middleBlock = GetLocalFileByteArray(stream, startDownloadPositionForMiddleBlock, MiddleBlockSize);

                endBlock = GetLocalFileByteArray(stream, startDownloadPositionForEndBlock, EndBlockSize);
            }
            else
            {
                startBlock = GetLocalFileByteArray(stream, 0, size);
            }

            return CreateHashForByteArray(startBlock, middleBlock, endBlock);
        }

        public static long GetFileLength(string fileName)
        {
            return new FileInfo(fileName).Length;
        }

        public static DateTime GetFileLastWriteTime(string fileName)
        {
            // Sometimes the file doesn't have a valid time

            var fi = new FileInfo(fileName);

            DateTime returnValue;

            try
            {
                returnValue = fi.LastWriteTime;
            }
            catch (ArgumentOutOfRangeException ex)
            {
                throw new ArgumentOutOfRangeException(string.Format("Couldn't read LastAccessTime on {0}", fileName), ex);
            }

            return returnValue;
        }

        /// <summary>
        /// Gets an MD5 hash of the first 16K, the 16K from the middle and last 16K of a file.
        /// </summary>
        /// <param name="path">
        /// The local file name.
        /// </param>
        /// <returns>
        /// An MD5 hash of the file or null if File doesn't exist or string.Empty if it has no size.
        /// </returns>
        public static string GetShortMd5HashFromFile(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            long size = new FileInfo(path).Length;

            if (size <= 0)
            {
                return string.Empty;
            }

            byte[] startBlock;
            byte[] middleBlock = null;
            byte[] endBlock = null;

            if (size >= StartBlockSize + MiddleBlockSize + EndBlockSize)
            {
                long startDownloadPositionForEndBlock = size - EndBlockSize;

                long startDownloadPositionForMiddleBlock = size / 2;

                startBlock = GetLocalFileByteArray(path, 0, StartBlockSize);

                middleBlock = GetLocalFileByteArray(path, startDownloadPositionForMiddleBlock, MiddleBlockSize);

                endBlock = GetLocalFileByteArray(path, startDownloadPositionForEndBlock, EndBlockSize);
            }
            else
            {
                startBlock = GetLocalFileByteArray(path, 0, size);
            }

            return CreateHashForByteArray(startBlock, middleBlock, endBlock);
        }

        public static void SendPushbulletMessage(string title, string message)
        {

            try
            {
                PushbulletClient client;

                User currentUserInformation;

                client = new PushbulletClient("o.7DsY57f3IS7FXOejdR8ncoIXvU3n2yF0");

                currentUserInformation = client.CurrentUsersInformation();

                if (currentUserInformation != null)
                {
                    PushNoteRequest request = new PushNoteRequest()
                    {
                        Email = currentUserInformation.Email,
                        Title = title,
                        Body = message
                    };

                    PushResponse response = client.PushNote(request);
                }
            }
            catch (Exception)
            {
                // we ignore any pushbullet problems
            }
        }

        public static void Log(string logFilePath, string text, params object[] args)
        {
            string textToWrite = DateTime.Now.ToString("dd-MM-yy HH:mm:ss") + " : " + string.Format(text, args);

            Console.WriteLine(textToWrite);
            if (!string.IsNullOrEmpty(logFilePath))
            {
                EnsureDirectories(logFilePath);
                File.AppendAllLines(logFilePath, new[] { textToWrite });
            }
        }

        public static void Log(string logFilePath, BackupAction backupAction, string text, params object[] args)
        {
            Log(logFilePath, text, args);
            Utils.SendPushbulletMessage("Backup - " + System.Enum.GetName(typeof(BackupAction), backupAction), string.Format(text, args));
        }

        #endregion

        #region Methods

        /// <summary>
        /// The byte array to string.
        /// </summary>
        /// <param name="value">
        /// The value.
        /// </param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// </exception>
        private static string ByteArrayToString(byte[] value)
        {
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }

            return ByteArrayToString(value, 0, value.Length);
        }

        /// <summary>
        /// The byte array to string.
        /// </summary>
        /// <param name="value">
        /// The value.
        /// </param>
        /// <param name="startIndex">
        /// The start index.
        /// </param>
        /// <param name="length">
        /// The length.
        /// </param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// </exception>
        /// <exception cref="ArgumentException">
        /// </exception>
        private static string ByteArrayToString(byte[] value, int startIndex, int length)
        {
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }

            if (startIndex < 0 || startIndex >= value.Length && startIndex > 0)
            {
                throw new ArgumentOutOfRangeException("startIndex");
            }

            if (length < 0)
            {
                throw new ArgumentOutOfRangeException("length");
            }

            if (startIndex > value.Length - length)
            {
                throw new ArgumentException("length");
            }

            if (length == 0)
            {
                return string.Empty;
            }

            if (length > 715827882)
            {
                throw new ArgumentOutOfRangeException("length");
            }

            int length1 = length * 2;
            var chArray = new char[length1];
            int num1 = startIndex;
            int index = 0;
            while (index < length1)
            {
                byte num2 = value[num1++];
                chArray[index] = GetLowercaseHexValue(num2 / 16);
                chArray[index + 1] = GetLowercaseHexValue(num2 % 16);
                index += 2;
            }

            return new string(chArray, 0, chArray.Length);
        }

        /// <summary>
        /// Creates a hash for the two byte arrays passed in.
        /// </summary>
        /// <param name="firstByteArray">
        /// The start byte array.
        /// </param>
        /// <param name="endByteArray">
        /// The end byte array.
        /// </param>
        /// <returns>
        /// A String of the hash.
        /// </returns>
        private static string CreateHashForByteArray(byte[] firstByteArray, byte[] endByteArray)
        {
            byte[] byteArrayToHash = endByteArray == null
                                         ? new byte[firstByteArray.Length]
                                         : new byte[firstByteArray.Length + endByteArray.Length];

            Buffer.BlockCopy(firstByteArray, 0, byteArrayToHash, 0, firstByteArray.Length);
            if (endByteArray != null)
            {
                Buffer.BlockCopy(endByteArray, 0, byteArrayToHash, firstByteArray.Length, endByteArray.Length);
            }

            return ByteArrayToString(Md5.ComputeHash(byteArrayToHash));
        }

        /// <summary>
        /// The ensure path has a terminating separator.
        /// </summary>
        /// <param name="path">
        /// The path.
        /// </param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        private static string EnsurePathHasATerminatingSeparator(string path)
        {
            if (path.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }

        /// <summary>
        /// The get local file byte array.
        /// </summary>
        /// <param name="fileStream">
        /// The file stream.
        /// </param>
        /// <param name="offset">
        /// The offset.
        /// </param>
        /// <param name="byteCountToReturn">
        /// The byte count to return.
        /// </param>
        /// <returns>
        /// The <see cref="byte[]"/>.
        /// </returns>
        private static byte[] GetLocalFileByteArray(FileStream fileStream, long offset, long byteCountToReturn)
        {
            fileStream.Seek(offset, SeekOrigin.Begin);

            var buffer = new byte[byteCountToReturn];

            int count;
            int sum = 0;
            int length = Convert.ToInt32(byteCountToReturn);
            while ((count = fileStream.Read(buffer, sum, length - sum)) > 0)
            {
                sum += count; // sum is a buffer offset for next reading
            }

            if (sum < byteCountToReturn)
            {
                var byteArray = new byte[sum];
                Buffer.BlockCopy(buffer, 0, byteArray, 0, sum);
                return byteArray;
            }

            return buffer;
        }

        /// <summary>
        /// The get file byte array.
        /// </summary>
        /// <param name="fileName">
        /// The file name.
        /// </param>
        /// <param name="offset">
        /// The offset.
        /// </param>
        /// <param name="byteCountToReturn">
        /// The byte count to return.
        /// </param>
        /// <returns>
        /// The <see cref="byte[]"/>.
        /// </returns>
        private static byte[] GetLocalFileByteArray(string fileName, long offset, long byteCountToReturn)
        {
            byte[] buffer;
            var fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            fileStream.Seek(offset, SeekOrigin.Begin);
            int sum = 0;
            try
            {
                buffer = new byte[byteCountToReturn];

                // count = fileStream.Read(buffer, 0, Convert.ToInt32(byteCountToReturn));
                int length = Convert.ToInt32(byteCountToReturn);
                int count;
                while ((count = fileStream.Read(buffer, sum, length - sum)) > 0)
                {
                    sum += count; // sum is a buffer offset for next reading
                }
            }
            finally
            {
                fileStream.Close();
            }

            if (sum < byteCountToReturn)
            {
                var byteArray = new byte[sum];
                Buffer.BlockCopy(buffer, 0, byteArray, 0, sum);
                return byteArray;
            }

            return buffer;
        }

        /// <summary>
        /// The get lowercase hex value.
        /// </summary>
        /// <param name="i">
        /// The i.
        /// </param>
        /// <returns>
        /// The <see cref="char"/>.
        /// </returns>
        private static char GetLowercaseHexValue(int i)
        {
            if (i < 10)
            {
                return (char)(i + 48);
            }

            return (char)(i - 10 + 65 + 32);
        }

        #endregion

        public static bool GetDiskFreeSpace(string folderName, out long freespace)
        {
            freespace = 0;
            if (string.IsNullOrEmpty(folderName))
            {
                throw new ArgumentNullException("folderName");
            }

            if (!folderName.EndsWith("\\"))
            {
                folderName += '\\';
            }

            ulong free = 0, dummy1 = 0, dummy2 = 0;

            if (Utils.GetDiskFreeSpaceEx(folderName, out free, out dummy1, out dummy2))
            {
                freespace = Convert.ToInt64(free);
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}