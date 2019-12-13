/**
 * EASY BUSINESS INTERFACE SERVICES.
 * Copyright (c) 2010-2014 EASY SOFTWARE AG, All rights reserved
 */
using System.IO;
using System.Linq;

namespace Easy.Business.Samples
{

    /// <summary>
    /// EASY BUSINESS INTERFACE SERVICES
    /// This sample demonstrates how to read and resolve attachments contained by a document.
    /// Since: 1.1.13
    /// </summary>
    static class BlobHelper
    {

        /// <summary>
        /// Return all blobs of the given document.
        /// </summary>
        /// <param name="doc">An document resolved by Session.GetDocument</param>
        /// <returns>All blobs of the given document.</returns>
        public static BlobContent[] GetAllBlobs(Document doc)
        {
            return doc.Items.OfType<BlobContent>().ToArray();
        }

        /// <summary>
        /// Returns the first blob which the file name or name is matched.
        /// </summary>
        /// <param name="doc">An document resolved by Session.GetDocument</param>
        /// <param name="fileNameOrName">The file name or blob item name.</param>
        /// <returns>The first blob which the file name or name is matched.</returns>
        public static BlobContent FindBlob(Document doc, string fileNameOrName)
        {
            var blob = doc.GetFirstBlobByFileName(fileNameOrName);
            return blob ?? doc.GetFirstBlob(fileNameOrName);
        }

        /// <summary>
        /// This method stores the data of a blob to an output stream.
        /// </summary>
        /// <param name="blob">The blob item.</param>
        /// <param name="outputStream">The stream to write the data into. Stream must enable to write data. The data will be appended to the current position of the stream.</param>
        public static void StoreBlobToStream(BlobContent blob, Stream outputStream)
        {
            var buf = new byte[1024 * 1024]; // 1k sized buffer to store stream data.

            // Read the data from the item. The "using" construct allows to ensure the used stream will be closed after processing.
            using (var blobInputStream = blob.Stream)
            {
                int len; // helper variable
                while ((len = blobInputStream.Read(buf, 0, buf.Length)) > 0)
                {
                    outputStream.Write(buf, 0, len);
                }
            }
        }

        /// <summary>
        /// This method stores the data of a blob to a local stored file.
        /// </summary>
        /// <param name="blob">The blob item.</param>
        /// <param name="outputFileName">The name of a file to write the data into. An existing file will be overwritten.</param>
        public static void StoreBlobToFile(BlobContent blob, string outputFileName)
        {
            // Create a file stream. The "using" construct allows to ensure the used stream will be closed after processing.
            string directory = new FileInfo(outputFileName).Directory.ToString();

            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            using (var fileStream = new FileStream(outputFileName, FileMode.Create, FileAccess.Write))
                StoreBlobToStream(blob, fileStream);
        }
    }
}
