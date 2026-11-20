using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCP.Plugin.Utils;
using RevitMCP.Shared.Commands;

namespace RevitMCP.Plugin.Commands
{
    public class ExportViewImageHandler
    {
        public ExportViewImageResult Execute(UIApplication app, ExportViewImageRequest request)
        {
            var result = new ExportViewImageResult
            {
                Format = string.Equals(request.Format, "jpeg", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(request.Format, "jpg", StringComparison.OrdinalIgnoreCase) ? "jpeg" : "png",
                MimeType = string.Equals(request.Format, "jpeg", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(request.Format, "jpg", StringComparison.OrdinalIgnoreCase) ? "image/jpeg" : "image/png"
            };

            var doc = app.ActiveUIDocument?.Document;
            if (doc == null)
            {
                result.Success = false;
                result.ErrorMessage = "No active document open.";
                return result;
            }

            View? targetView = null;
            if (request.ViewId.HasValue)
            {
                targetView = doc.GetElement(ElementIdExtensions.FromLongId(request.ViewId.Value)) as View;
                if (targetView == null)
                {
                    result.Success = false;
                    result.ErrorMessage = $"View with ID {request.ViewId.Value} not found.";
                    return result;
                }
            }
            else if (!string.IsNullOrEmpty(request.ViewName))
            {
                targetView = new FilteredElementCollector(doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .FirstOrDefault(v => !v.IsTemplate && string.Equals(v.Name, request.ViewName, StringComparison.OrdinalIgnoreCase));

                if (targetView == null)
                {
                    result.Success = false;
                    result.ErrorMessage = $"View with name '{request.ViewName}' not found.";
                    return result;
                }
            }
            else
            {
                targetView = doc.ActiveView;
                if (targetView == null)
                {
                    result.Success = false;
                    result.ErrorMessage = "No active view found.";
                    return result;
                }
            }

            if (targetView.IsTemplate)
            {
                result.Success = false;
                result.ErrorMessage = $"View '{targetView.Name}' is a view template and cannot be exported to an image.";
                return result;
            }

            result.ViewId = targetView.Id.ToLongId();
            result.ViewName = targetView.Name;

            var tempDir = Path.Combine(Path.GetTempPath(), "RevitMCP_View_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var filePathPrefix = Path.Combine(tempDir, "view_export");

            try
            {
                bool isJpeg = result.Format == "jpeg";
                var imgOpt = new ImageExportOptions
                {
                    ExportRange = ExportRange.SetOfViews,
                    FilePath = filePathPrefix,
                    FitDirection = FitDirectionType.Horizontal,
                    HLRandWFViewsFileType = isJpeg ? ImageFileType.JPEGLossless : ImageFileType.PNG,
                    ShadowViewsFileType = isJpeg ? ImageFileType.JPEGLossless : ImageFileType.PNG,
                    ImageResolution = ImageResolution.DPI_150,
                    PixelSize = request.PixelSize > 0 ? request.PixelSize : 1024,
                    ZoomType = ZoomFitType.FitToPage
                };

                imgOpt.SetViewsAndSheets(new List<ElementId> { targetView.Id });
                doc.ExportImage(imgOpt);

                var generatedFiles = Directory.GetFiles(tempDir);
                if (generatedFiles.Length == 0)
                {
                    result.Success = false;
                    result.ErrorMessage = "Revit finished ExportImage but no image file was produced.";
                    return result;
                }

                var imageFile = generatedFiles[0];
                byte[] bytes = File.ReadAllBytes(imageFile);
                result.FileSizeBytes = bytes.Length;

                if (result.Format == "png" && bytes.Length >= 24)
                {
                    // Big endian width and height in PNG header: offsets 16..19 and 20..23
                    result.Width = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
                    result.Height = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];
                }

                if (!string.IsNullOrEmpty(request.OutputFilePath))
                {
                    var destDir = Path.GetDirectoryName(request.OutputFilePath);
                    if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                    }
                    File.Copy(imageFile, request.OutputFilePath, true);
                    result.SavedPath = request.OutputFilePath;
                }

                if (request.ReturnBase64)
                {
                    result.Base64Data = Convert.ToBase64String(bytes);
                }

                result.Success = true;
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Failed to export view image: {ex.Message}";
                return result;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch { }
            }
        }
    }
}
