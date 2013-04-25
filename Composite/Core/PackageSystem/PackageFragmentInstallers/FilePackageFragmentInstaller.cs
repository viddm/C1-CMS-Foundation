﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Composite.Core.Extensions;
using Composite.Core.IO;
using Composite.Core.Logging;
using Composite.Core.ResourceSystem;
using Composite.Core.Types;
using Composite.Core.Xml;
using Composite.Data;
using System.Reflection;
using System.Threading.Tasks;


namespace Composite.Core.PackageSystem.PackageFragmentInstallers
{
    /// <summary>    
    /// </summary>
    /// <exclude />
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public sealed class FilePackageFragmentInstaller : BasePackageFragmentInstaller
    {
        private List<FileToCopy> _filesToCopy = null;
        private List<string> _directoriesToDelete = null;


        /// <exclude />
        public override IEnumerable<PackageFragmentValidationResult> Validate()
        {
            List<PackageFragmentValidationResult> validationResult = new List<PackageFragmentValidationResult>();

            if (this.Configuration.Where(f => f.Name == "Files").Count() > 1)
            {
                validationResult.AddFatal(GetText("FilePackageFragmentInstaller.OnlyOneFilesElement"), this.ConfigurationParent);
                return validationResult;
            }

            if (this.Configuration.Where(f => f.Name == "Directories").Count() > 1)
            {
                validationResult.AddFatal(GetText("FilePackageFragmentInstaller.OnlyOneDirectoriesElement"), this.ConfigurationParent);
                return validationResult;
            }

            XElement filesElement = this.Configuration.SingleOrDefault(f => f.Name == "Files");
            XElement directoriesElement = this.Configuration.SingleOrDefault(f => f.Name == "Directories");

            _filesToCopy = new List<FileToCopy>();
            _directoriesToDelete = new List<string>();

            if (filesElement != null)
            {
                foreach (XElement fileElement in filesElement.Elements("File"))
                {
                    XAttribute sourceFilenameAttribute = fileElement.Attribute("sourceFilename");
                    XAttribute targetFilenameAttribute = fileElement.Attribute("targetFilename");

                    if (sourceFilenameAttribute == null)
                    {
                        validationResult.AddFatal(GetText("FilePackageFragmentInstaller.MissingAttribute").FormatWith("sourceFilename"), fileElement); 
                        continue;
                    }

                    if (targetFilenameAttribute == null)
                    {
                        validationResult.AddFatal(GetText("FilePackageFragmentInstaller.MissingAttribute").FormatWith("targetFilename"), fileElement); 
                        continue;
                    }

                    XAttribute allowOverwriteAttribute = fileElement.Attribute("allowOverwrite");
                    XAttribute assemblyLoadAttribute = fileElement.Attribute("assemblyLoad");
                    XAttribute deleteTargetDirectoryAttribute = fileElement.Attribute("deleteTargetDirectory");
                    XAttribute onlyUpdateAttribute = fileElement.Attribute("onlyUpdate");

                    if (deleteTargetDirectoryAttribute != null)
                    {
                        validationResult.AddFatal(GetText("FilePackageFragmentInstaller.DeleteTargetDirectoryNotAllowed"), fileElement);
                        continue;
                    }

                    bool allowOverwrite = false;
                    if (allowOverwriteAttribute != null)
                    {
                        if (allowOverwriteAttribute.TryGetBoolValue(out allowOverwrite) == false)
                        {
                            validationResult.AddFatal(GetText("FilePackageFragmentInstaller.WrongAttributeBoolFormat"), allowOverwriteAttribute);
                            continue;
                        }
                    }

                    bool loadAssembly = false;
                    if (assemblyLoadAttribute != null)
                    {
                        if (assemblyLoadAttribute.TryGetBoolValue(out loadAssembly) == false)
                        {
                            validationResult.AddFatal(GetText("FilePackageFragmentInstaller.WrongAttributeBoolFormat"), assemblyLoadAttribute);
                            continue;
                        }
                    }

                    bool onlyUpdate = false;
                    if (onlyUpdateAttribute != null)
                    {
                        if (onlyUpdateAttribute.TryGetBoolValue(out onlyUpdate) == false)
                        {
                            validationResult.AddFatal(GetText("FilePackageFragmentInstaller.WrongAttributeBoolFormat"), onlyUpdateAttribute);
                            continue;
                        }
                    }

                    string sourceFilename = sourceFilenameAttribute.Value;
                    if (this.InstallerContext.ZipFileSystem.ContainsFile(sourceFilename) == false)
                    {
                        validationResult.AddFatal(GetText("FilePackageFragmentInstaller.MissingFile").FormatWith(sourceFilename), sourceFilenameAttribute);
                        continue;
                    }

                    if ((loadAssembly == true) && (onlyUpdate == true))
                    {
                        validationResult.AddFatal(GetText("FilePackageFragmentInstaller.OnlyUpdateNotAllowedWithLoadAssemlby"), onlyUpdateAttribute);
                        continue;
                    }

                    string targetFilename = PathUtil.Resolve(targetFilenameAttribute.Value);
                    if (C1File.Exists(targetFilename) == true)
                    {
                        if ((allowOverwrite == false) && (onlyUpdate == false))
                        {
                            validationResult.AddFatal(GetText("FilePackageFragmentInstaller.FileExists").FormatWith(targetFilename), targetFilenameAttribute);
                            continue;
                        }

                        if (((C1File.GetAttributes(targetFilename) & FileAttributes.ReadOnly) > 0) && (allowOverwrite == false))
                        {
                            validationResult.AddFatal(GetText("FilePackageFragmentInstaller.FileReadOnly").FormatWith(targetFilename), targetFilenameAttribute);
                            continue;
                        }
                    }
                    else if (onlyUpdate == true)
                    {
                        LoggingService.LogVerbose("FilePackageFragmentInstaller", string.Format("Skipping updating of the file '{0}' because it does not exist", targetFilename));
                        continue; // Target file does not, so skip this
                    }

                    FileToCopy fileToCopy = new FileToCopy
                    {
                        SourceFilename = sourceFilename,
                        TargetRelativeFilePath = targetFilenameAttribute.Value,
                        TargetFilePath = targetFilename,
                        AllowOverwrite = allowOverwrite
                    };

                    _filesToCopy.Add(fileToCopy);

                    if (loadAssembly == true)
                    {
                        string tempFilename = Path.Combine(this.InstallerContext.TempDirectory, Path.GetFileName(targetFilename));

                        this.InstallerContext.ZipFileSystem.WriteFileToDisk(sourceFilename, tempFilename);

                        PackageAssemblyHandler.AddAssembly(tempFilename);
                    }
                }
            }

            if (directoriesElement != null)
            {
                foreach (XElement directoryElement in directoriesElement.Elements("Directory"))
                {
                    XAttribute sourceDirectoryAttribute = directoryElement.Attribute("sourceDirectory");
                    XAttribute targetDirectoryAttribute = directoryElement.Attribute("targetDirectory");

                    if (sourceDirectoryAttribute == null)
                    {
                        validationResult.AddFatal(GetText("FilePackageFragmentInstaller.MissingAttribute").FormatWith("sourceDirectory"), directoryElement); 
                        continue;
                    }

                    if (targetDirectoryAttribute == null)
                    {
                        validationResult.AddFatal(GetText("FilePackageFragmentInstaller.MissingAttribute").FormatWith("targetDirectory"), directoryElement); 
                        continue;
                    }


                    XAttribute allowOverwriteAttribute = directoryElement.Attribute("allowOverwrite");
                    XAttribute assemblyLoadAttribute = directoryElement.Attribute("assemblyLoad");
                    XAttribute deleteTargetDirectoryAttribute = directoryElement.Attribute("deleteTargetDirectory");
                    XAttribute onlyUpdateAttribute = directoryElement.Attribute("onlyUpdate");

                    if (assemblyLoadAttribute != null)
                    {
                        validationResult.AddFatal(GetText("FilePackageFragmentInstaller.AssemblyLoadNotAllowed"), directoryElement);
                        continue;
                    }

                    if (onlyUpdateAttribute != null)
                    {
                        validationResult.AddFatal(GetText("FilePackageFragmentInstaller.OnlyUpdateNotAllowed"), directoryElement);
                        continue;
                    }


                    bool allowOverwrite = false;
                    if (allowOverwriteAttribute != null)
                    {
                        if (allowOverwriteAttribute.TryGetBoolValue(out allowOverwrite) == false)
                        {
                            validationResult.AddFatal(GetText("FilePackageFragmentInstaller.WrongAttributeBoolFormat"), allowOverwriteAttribute);
                            continue;
                        }
                    }

                    bool deleteTargetDirectory = false;
                    if (deleteTargetDirectoryAttribute != null)
                    {
                        if (deleteTargetDirectoryAttribute.TryGetBoolValue(out deleteTargetDirectory) == false)
                        {
                            validationResult.AddFatal(GetText("FilePackageFragmentInstaller.WrongAttributeBoolFormat"), deleteTargetDirectoryAttribute);
                            continue;
                        }
                    }

                    string sourceDirectory = sourceDirectoryAttribute.Value;
                    if (this.InstallerContext.ZipFileSystem.ContainsDirectory(sourceDirectory) == false)
                    {
                        validationResult.AddFatal(GetText("FilePackageFragmentInstaller.MissingDirectory").FormatWith(sourceDirectory), sourceDirectoryAttribute);
                        continue;
                    }

                    string targetDirectory = PathUtil.Resolve(targetDirectoryAttribute.Value);

                    if (deleteTargetDirectory == true)
                    {
                        if (C1Directory.Exists(targetDirectory) == true)
                        {
                            _directoriesToDelete.Add(targetDirectory);
                        }
                    }

                    foreach (string sourceFilename in this.InstallerContext.ZipFileSystem.GetFilenames(sourceDirectory))
                    {
                        string resolvedSourceFilename = sourceFilename.Remove(0, sourceDirectory.Length);
                        if (resolvedSourceFilename.StartsWith("/") == true)
                        {
                            resolvedSourceFilename = resolvedSourceFilename.Remove(0, 1);
                        }

                        string targetFilename = Path.Combine(targetDirectory, resolvedSourceFilename);

                        if ((C1File.Exists(targetFilename) == true) && (deleteTargetDirectory == false) && (allowOverwrite == false))
                        {
                            validationResult.AddFatal(GetText("FilePackageFragmentInstaller.FileExists").FormatWith(targetFilename), targetDirectoryAttribute);
                            continue;
                        }

                        FileToCopy fileToCopy = new FileToCopy
                        {
                            SourceFilename = sourceFilename,
                            TargetRelativeFilePath = Path.Combine(targetDirectoryAttribute.Value, resolvedSourceFilename),
                            TargetFilePath = targetFilename,
                            AllowOverwrite = allowOverwrite
                        };
                        _filesToCopy.Add(fileToCopy);
                    }
                }
            }

            if (validationResult.Count > 0)
            {
                _filesToCopy = null;
                _directoriesToDelete = null;
            }

            return validationResult;
        }

        private static string GetText(string stringId)
        {
            return GetResourceString(stringId);
        }

        /// <exclude />
        public override IEnumerable<XElement> Install()
        {
            if (_filesToCopy == null) throw new InvalidOperationException("FilePackageFragmentInstaller has not been validated");

            foreach (string directoryToDelete in _directoriesToDelete)
            {
                Directory.Delete(directoryToDelete, true);
            }


            foreach (string targetDirectory in _filesToCopy.Select( f=> Path.GetDirectoryName(f.TargetFilePath)).Distinct())
            {
                if (Directory.Exists(targetDirectory) == false)
                {
                    LoggingService.LogVerbose("FilePackageFragmentInstaller", string.Format("Creating directory '{0}'", targetDirectory));
                    Directory.CreateDirectory(targetDirectory);
                }
            }

            List<XElement> fileElements = new List<XElement>();
            Parallel.ForEach(_filesToCopy, fileToCopy =>
            {
                LoggingService.LogVerbose("FilePackageFragmentInstaller", string.Format("Installing the file '{0}' to the target filename '{1}'", fileToCopy.SourceFilename, fileToCopy.TargetFilePath));

                if ((C1File.Exists(fileToCopy.TargetFilePath) && ((C1File.GetAttributes(fileToCopy.TargetFilePath) & FileAttributes.ReadOnly) > 0) && (fileToCopy.AllowOverwrite)))
                {
                    FileUtils.RemoveReadOnly(fileToCopy.TargetFilePath);
                }

                this.InstallerContext.ZipFileSystem.WriteFileToDisk(fileToCopy.SourceFilename, fileToCopy.TargetFilePath);

                if (fileToCopy.TargetFilePath.StartsWith(Path.Combine(PathUtil.BaseDirectory, "Bin"), StringComparison.InvariantCultureIgnoreCase)
                    && fileToCopy.TargetFilePath.EndsWith(".dll", StringComparison.InvariantCultureIgnoreCase))
                {
                    Assembly assembly = Assembly.LoadFrom(fileToCopy.TargetFilePath);
                    DataTypeTypesManager.AddNewAssembly(assembly);
                }

                XElement fileElement = new XElement("File", new XAttribute("filename", fileToCopy.TargetRelativeFilePath));

                fileElements.Add(fileElement);
            });

            yield return new XElement("Files", fileElements);
        }



        private sealed class FileToCopy
        {
            public string SourceFilename { get; set; }
            public string TargetRelativeFilePath { get; set; }
            public string TargetFilePath { get; set; }
            public bool AllowOverwrite { get; set; }
        }
    }
}
