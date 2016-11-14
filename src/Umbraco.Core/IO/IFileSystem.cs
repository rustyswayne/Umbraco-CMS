﻿using System;
using System.Collections.Generic;
using System.IO;

namespace Umbraco.Core.IO
{
	public interface IFileSystem
    {
        IEnumerable<string> GetDirectories(string path);

        void DeleteDirectory(string path);

        void DeleteDirectory(string path, bool recursive);

        bool DirectoryExists(string path);

        void AddFile(string path, Stream stream);

        void AddFile(string path, Stream stream, bool overrideIfExists);

        IEnumerable<string> GetFiles(string path);

        IEnumerable<string> GetFiles(string path, string filter);

        Stream OpenFile(string path);

        void DeleteFile(string path);

        bool FileExists(string path);

        string GetRelativePath(string fullPathOrUrl);

        string GetFullPath(string path);

        string GetUrl(string path);

        DateTimeOffset GetLastModified(string path);

        DateTimeOffset GetCreated(string path);

        long GetSize(string path);

        // TODO: implement these
        //
        //void CreateDirectory(string path);
        //
        //// move or rename, directory or file
        //void Move(string source, string target);
    }
}
