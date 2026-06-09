using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Configuration;
using System.IO;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace tfoodiesBackend.Commons
{
    public static class AzureBlob
    {
        public static string UploadFile(string containerName, HttpPostedFileBase file, string newfilename = null, string groupName = null)
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["azure.blob.connectionstring"]);

            // Create the blob client.   
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

            // Retrieve a reference to a container.    
            CloudBlobContainer container = blobClient.GetContainerReference(containerName.ToLower());

            // Create the container if it doesn't already exist.   
            container.CreateIfNotExists();

            container.SetPermissions(
                new BlobContainerPermissions
                {
                    PublicAccess = BlobContainerPublicAccessType.Blob
                }
            );

            string fileName = (newfilename != null) ? newfilename : file.FileName;
            fileName = (groupName != null) ? groupName + "/" + fileName : fileName;
            CloudBlockBlob blockBlob = container.GetBlockBlobReference(fileName);

            blockBlob.Properties.ContentType = file.ContentType;
            blockBlob.UploadFromStream(file.InputStream);

            return blockBlob.Uri.AbsoluteUri;
        }

        public static string UploadByteFile(string containerName, byte[] file, string newfilename, string filetype, string groupName = null)
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["azure.blob.connectionstring"]);

            // Create the blob client.   
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

            // Retrieve a reference to a container.    
            CloudBlobContainer container = blobClient.GetContainerReference(containerName.ToLower());

            // Create the container if it doesn't already exist.   
            container.CreateIfNotExists();

            container.SetPermissions(
                new BlobContainerPermissions
                {
                    PublicAccess = BlobContainerPublicAccessType.Blob
                }
            );

            MemoryStream ms = new MemoryStream(file, false);

            newfilename = (groupName != null) ? groupName + "/" + newfilename : newfilename;
            CloudBlockBlob blockBlob = container.GetBlockBlobReference(newfilename);

            blockBlob.Properties.ContentType = filetype;
            blockBlob.UploadFromStream(ms);

            return blockBlob.Uri.AbsoluteUri;
        }

        public static void DeleteFile(string containerName, string filename, string groupName = null)
        {
            if (filename != null)
            {
                CloudStorageAccount storageAccount = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["azure.blob.connectionstring"]);

                // Create the blob client.   
                CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

                // Retrieve a reference to a container.    
                CloudBlobContainer container = blobClient.GetContainerReference(containerName.ToLower());

                filename = (groupName != null) ? groupName + "/" + filename : filename;
                CloudBlockBlob blockBlob = container.GetBlockBlobReference(filename);

                blockBlob.DeleteIfExists();
            }
        }
    }
}