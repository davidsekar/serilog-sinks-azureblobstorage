﻿using System;
using System.Threading.Tasks;
using FakeItEasy;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Auth;
using Microsoft.Azure.Storage.Blob;
using Serilog.Sinks.AzureBlobStorage.AzureBlobProvider;
using Xunit;

namespace Serilog.Sinks.AzureBlobStorage.UnitTest
{
    /// <summary>
    /// These tests need updates.  In v10 of the Windows Azure Storage libraries, CreateCloudBlobClient() is now a static extension method, so it can no longer be mocked.
    /// </summary>
    /// 
    public class DefaultCloudBlobProviderUT
    {
        private readonly CloudStorageAccount storageAccount = A.Fake<CloudStorageAccount>(opt => opt.WithArgumentsForConstructor(new object[] { new StorageCredentials(), "account", "suffix.blobs.com", true }));
        private readonly CloudBlobClient blobClient = A.Fake<CloudBlobClient>(opt => opt.WithArgumentsForConstructor(new object[] { new Uri("https://account.suffix.blobs.com"), new StorageCredentials(), null }));        

        private readonly string blobContainerName = "logcontainer";
        private readonly CloudBlobContainer blobContainer = A.Fake<CloudBlobContainer>(opt => opt.WithArgumentsForConstructor(new object[] { new Uri("https://account.suffix.blobs.com/logcontainer") }));

        private readonly DefaultCloudBlobProvider defaultCloudBlobProvider = new DefaultCloudBlobProvider();
        
        public DefaultCloudBlobProviderUT()
        {
            //A.CallTo(() => storageAccount.CreateCloudBlobClient()).Returns(blobClient);            //fails because is static
            //A.CallTo(() => blobClient.GetContainerReference(blobContainerName)).Returns(blobContainer);
            //A.CallTo(() => blobContainer.CreateIfNotExistsAsync()).Returns(Task.FromResult(true));
        }

        private CloudAppendBlob SetupCloudAppendBlobReference(string blobName, int blockCount)
        {
            CloudAppendBlob cloudAppendBlob = A.Fake<CloudAppendBlob>(opt => opt.WithArgumentsForConstructor(new object[] { new Uri("https://account.suffix.blobs.com/logcontainer/" + blobName) }));

            SetCloudBlobBlockCount(cloudAppendBlob, blockCount);

            A.CallTo(() => cloudAppendBlob.Name).Returns(blobName);
            A.CallTo(() => cloudAppendBlob.CreateOrReplaceAsync(A<AccessCondition>.Ignored, null,null)).Returns(Task.FromResult(true));
            A.CallTo(() => cloudAppendBlob.FetchAttributesAsync()).Returns(Task.FromResult(true));

            A.CallTo(() => blobContainer.GetAppendBlobReference(blobName)).Returns(cloudAppendBlob);

            return cloudAppendBlob;
        }

        private void SetCloudBlobBlockCount(CloudAppendBlob cloudAppendBlob, int newBlockCount)
        {
            cloudAppendBlob.Properties.GetType().GetProperty(nameof(BlobProperties.AppendBlobCommittedBlockCount)).SetValue(cloudAppendBlob.Properties, newBlockCount, null);
        }
        
        [SkippableFact(DisplayName = "Should return same cloudblob if blobname is unchanged and max blocks has not been reached during.")]
        public async Task ReturnSameBlobReferenceIfNameNotChangedAndMaxBlocksNotReached()
        {
            Skip.If(true);
            const string blobName = "SomeBlob.log";
            CloudAppendBlob cloudAppendBlob = SetupCloudAppendBlobReference(blobName, 0);

            CloudAppendBlob firstRequest = await defaultCloudBlobProvider.GetCloudBlobAsync(storageAccount, blobContainerName, blobName, true);

            //Update blockcount to a value below the max block count
            SetCloudBlobBlockCount(firstRequest, 1000);

            CloudAppendBlob secondRequest = await defaultCloudBlobProvider.GetCloudBlobAsync(storageAccount, blobContainerName, blobName, true);

            Assert.Same(firstRequest, secondRequest);
        }
        
        [SkippableFact(DisplayName = "Should return a rolled cloudblob if blobname is unchanged but max blocks has been reached during.")]
        public async Task ReturnRolledBlobReferenceIfNameNotChangedAndMaxBlocksReached()
        {
            Skip.If(true);
            const string blobName = "SomeBlob.log";
            const string rolledBlobName = "SomeBlob-001.log";
            CloudAppendBlob cloudAppendBlob = SetupCloudAppendBlobReference(blobName, 40000);

            CloudAppendBlob firstRequest = await defaultCloudBlobProvider.GetCloudBlobAsync(storageAccount, blobContainerName, blobName, true);

            //Update blockcount to a value below the max block count
            SetCloudBlobBlockCount(firstRequest, 50000);

            //setup the rolled cloudblob
            CloudAppendBlob rolledCloudAppendBlob = SetupCloudAppendBlobReference(rolledBlobName, 0);

            CloudAppendBlob secondRequest = await defaultCloudBlobProvider.GetCloudBlobAsync(storageAccount, blobContainerName, blobName, true);

            Assert.NotSame(firstRequest, secondRequest);
            Assert.Equal(blobName, firstRequest.Name);
            Assert.Equal(rolledBlobName, secondRequest.Name);
        }
        
        [SkippableFact(DisplayName = "Should return a rolled cloudblob on init if first blobs already reached the max block count.")]
        public async Task ReturnRolledBlobReferenceOnInitIfMaxBlocksReached()
        {
            Skip.If(true);
            const string blobName = "SomeBlob.log";
            const string firstRolledBlobName = "SomeBlob-001.log";
            const string secondRolledBlobName = "SomeBlob-002.log";
            CloudAppendBlob cloudAppendBlob = SetupCloudAppendBlobReference(blobName, 50000);
            CloudAppendBlob firstRolledCloudAppendBlob = SetupCloudAppendBlobReference(firstRolledBlobName, 50000);
            CloudAppendBlob secondRolledcloudAppendBlob = SetupCloudAppendBlobReference(secondRolledBlobName, 10000);

            CloudAppendBlob requestedBlob = await defaultCloudBlobProvider.GetCloudBlobAsync(storageAccount, blobContainerName, blobName, true);

            Assert.Equal(secondRolledBlobName, requestedBlob.Name);
        }
        
        [SkippableFact(DisplayName = "Should return a new cloudblob non-rolled, if previous cloudblob was rolled.")]
        public async Task ReturnNonRolledBlobReferenceOnInitIfPreviousCloudblobWasRolled()
        {
            Skip.If(true);
            const string blobName = "SomeBlob.log";
            const string rolledBlobName = "SomeBlob-001.log";
            const string newBlobName = "SomeNewBlob.log";

            CloudAppendBlob cloudAppendBlob = SetupCloudAppendBlobReference(blobName, 50000);
            CloudAppendBlob firstRolledCloudAppendBlob = SetupCloudAppendBlobReference(rolledBlobName, 40000);
            CloudAppendBlob newCloudAppendBlob = SetupCloudAppendBlobReference(newBlobName, 0);

            CloudAppendBlob requestedBlob = await defaultCloudBlobProvider.GetCloudBlobAsync(storageAccount, blobContainerName, blobName, true);
            CloudAppendBlob requestednewBlob = await defaultCloudBlobProvider.GetCloudBlobAsync(storageAccount, blobContainerName, newBlobName, true);

            Assert.Equal(rolledBlobName, requestedBlob.Name);
            Assert.Equal(newBlobName, requestednewBlob.Name);
        }

        [SkippableFact(DisplayName = "Should throw exception if container cannot be created and bypass is false.")]
        public async Task ThrowExceptionIfContainerCannotBeCreatedAndNoBypass()
        {
            Skip.If(true);
            const string blobName = "SomeBlob.log";
            CloudAppendBlob cloudAppendBlob = SetupCloudAppendBlobReference(blobName, 1000);

            A.CallTo(() => blobContainer.CreateIfNotExistsAsync()).Invokes(() => throw new StorageException());

            await Assert.ThrowsAnyAsync<Exception>(() => defaultCloudBlobProvider.GetCloudBlobAsync(storageAccount, blobContainerName, blobName, false));
        }

        [SkippableFact(DisplayName = "Should not throw exception if container cannot be 'CreatedIfNotExists' and bypass is true.")]
        public async Task DoNoThrowExceptionIfContainerCannotBeCreatedAndBypass()
        {
            Skip.If(true);
            const string blobName = "SomeBlob.log";
            CloudAppendBlob cloudAppendBlob = SetupCloudAppendBlobReference(blobName, 1000);

            A.CallTo(() => blobContainer.CreateIfNotExistsAsync()).Invokes(() => throw new StorageException());

            CloudAppendBlob blob = await defaultCloudBlobProvider.GetCloudBlobAsync(storageAccount, blobContainerName, blobName, true);
        }

        [SkippableFact(DisplayName = "Should throw exception if container cannot be 'CreatedIfNotExists' and bypass is true and container really does not exist.")]
        public async Task ThrowExceptionIfContainerCannotBeCreatedAndBypassAndContainerDoesNotExist()
        {
            Skip.If(true);
            A.CallTo(() => blobContainer.CreateIfNotExistsAsync()).Invokes(() => throw new StorageException());

            const string blobName = "SomeBlob.log";
            CloudAppendBlob cloudAppendBlob = SetupCloudAppendBlobReference(blobName, 1000);
            A.CallTo(() => cloudAppendBlob.CreateOrReplaceAsync(A<AccessCondition>.Ignored, null, null)).Invokes(() => throw new StorageException());

            await Assert.ThrowsAnyAsync<Exception>(() => defaultCloudBlobProvider.GetCloudBlobAsync(storageAccount, blobContainerName, blobName, true));
        }
    }
}
