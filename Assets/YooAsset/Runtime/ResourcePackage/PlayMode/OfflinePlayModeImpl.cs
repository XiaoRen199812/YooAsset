﻿using System;
using System.Collections;
using System.Collections.Generic;

namespace YooAsset
{
	internal class OfflinePlayModeImpl : IPlayMode, IBundleQuery
	{
		private PackageManifest _activeManifest;
		private ResourceAssist _assist;
		
		public readonly string PackageName;
		public PersistentManager Persistent
		{
			get { return _assist.Persistent; }
		}
		public CacheManager Cache
		{
			get { return _assist.Cache; }
		}


		public OfflinePlayModeImpl(string packageName)
		{
			PackageName = packageName;
		}

		/// <summary>
		/// 异步初始化
		/// </summary>
		public InitializationOperation InitializeAsync(ResourceAssist assist)
		{
			_assist = assist;

			var operation = new OfflinePlayModeInitializationOperation(this);
			OperationSystem.StartOperation(PackageName, operation);
			return operation;
		}

		// 查询相关
		private bool IsCachedPackageBundle(PackageBundle packageBundle)
		{
			return _assist.Cache.IsCached(packageBundle.CacheGUID);
		}

		#region IPlayModeServices接口
		public PackageManifest ActiveManifest
		{
			set
			{
				_activeManifest = value;
			}
			get
			{
				return _activeManifest;
			}
		}
		public void FlushManifestVersionFile()
		{
		}

		UpdatePackageVersionOperation IPlayMode.UpdatePackageVersionAsync(bool appendTimeTicks, int timeout)
		{
			var operation = new OfflinePlayModeUpdatePackageVersionOperation();
			OperationSystem.StartOperation(PackageName, operation);
			return operation;
		}
		UpdatePackageManifestOperation IPlayMode.UpdatePackageManifestAsync(string packageVersion, bool autoSaveVersion, int timeout)
		{
			var operation = new OfflinePlayModeUpdatePackageManifestOperation();
			OperationSystem.StartOperation(PackageName, operation);
			return operation;
		}
		PreDownloadContentOperation IPlayMode.PreDownloadContentAsync(string packageVersion, int timeout)
		{
			var operation = new OfflinePlayModePreDownloadContentOperation(this);
			OperationSystem.StartOperation(PackageName, operation);
			return operation;
		}

		ResourceDownloaderOperation IPlayMode.CreateResourceDownloaderByAll(int downloadingMaxNumber, int failedTryAgain, int timeout)
		{
			return ResourceDownloaderOperation.CreateEmptyDownloader(PackageName, downloadingMaxNumber, failedTryAgain, timeout);
		}
		ResourceDownloaderOperation IPlayMode.CreateResourceDownloaderByTags(string[] tags, int downloadingMaxNumber, int failedTryAgain, int timeout)
		{
			return ResourceDownloaderOperation.CreateEmptyDownloader(PackageName, downloadingMaxNumber, failedTryAgain, timeout);
		}
		ResourceDownloaderOperation IPlayMode.CreateResourceDownloaderByPaths(AssetInfo[] assetInfos, int downloadingMaxNumber, int failedTryAgain, int timeout)
		{
			return ResourceDownloaderOperation.CreateEmptyDownloader(PackageName, downloadingMaxNumber, failedTryAgain, timeout);
		}

		ResourceUnpackerOperation IPlayMode.CreateResourceUnpackerByAll(int upackingMaxNumber, int failedTryAgain, int timeout)
		{
			List<BundleInfo> unpcakList = GetUnpackListByAll(_activeManifest);
			var operation = new ResourceUnpackerOperation(PackageName, unpcakList, upackingMaxNumber, failedTryAgain, timeout);
			return operation;
		}
		private List<BundleInfo> GetUnpackListByAll(PackageManifest manifest)
		{
			List<PackageBundle> downloadList = new List<PackageBundle>(1000);
			foreach (var packageBundle in manifest.BundleList)
			{
				// 忽略缓存文件
				if (IsCachedPackageBundle(packageBundle))
					continue;

				downloadList.Add(packageBundle);
			}

			return BundleInfo.ConvertToUnpackInfos(_assist, downloadList);
		}

		ResourceUnpackerOperation IPlayMode.CreateResourceUnpackerByTags(string[] tags, int upackingMaxNumber, int failedTryAgain, int timeout)
		{
			List<BundleInfo> unpcakList = GetUnpackListByTags(_activeManifest, tags);
			var operation = new ResourceUnpackerOperation(PackageName, unpcakList, upackingMaxNumber, failedTryAgain, timeout);
			return operation;
		}
		private List<BundleInfo> GetUnpackListByTags(PackageManifest manifest, string[] tags)
		{
			List<PackageBundle> downloadList = new List<PackageBundle>(1000);
			foreach (var packageBundle in manifest.BundleList)
			{
				// 忽略缓存文件
				if (IsCachedPackageBundle(packageBundle))
					continue;

				// 查询DLC资源
				if (packageBundle.HasTag(tags))
				{
					downloadList.Add(packageBundle);
				}
			}

			return BundleInfo.ConvertToUnpackInfos(_assist, downloadList);
		}
		#endregion

		#region IBundleQuery接口
		private BundleInfo CreateBundleInfo(PackageBundle packageBundle)
		{
			if (packageBundle == null)
				throw new Exception("Should never get here !");

			// 查询沙盒资源
			if (IsCachedPackageBundle(packageBundle))
			{
				BundleInfo bundleInfo = new BundleInfo(_assist, packageBundle, BundleInfo.ELoadMode.LoadFromCache);
				return bundleInfo;
			}

			// 查询APP资源
			{
				BundleInfo bundleInfo = new BundleInfo(_assist, packageBundle, BundleInfo.ELoadMode.LoadFromStreaming);
				return bundleInfo;
			}
		}
		BundleInfo IBundleQuery.GetMainBundleInfo(AssetInfo assetInfo)
		{
			if (assetInfo.IsInvalid)
				throw new Exception("Should never get here !");

			// 注意：如果清单里未找到资源包会抛出异常！
			var packageBundle = _activeManifest.GetMainPackageBundle(assetInfo.AssetPath);
			return CreateBundleInfo(packageBundle);
		}
		BundleInfo[] IBundleQuery.GetAllDependBundleInfos(AssetInfo assetInfo)
		{
			if (assetInfo.IsInvalid)
				throw new Exception("Should never get here !");

			// 注意：如果清单里未找到资源包会抛出异常！
			var depends = _activeManifest.GetAllDependencies(assetInfo.AssetPath);
			List<BundleInfo> result = new List<BundleInfo>(depends.Length);
			foreach (var packageBundle in depends)
			{
				BundleInfo bundleInfo = CreateBundleInfo(packageBundle);
				result.Add(bundleInfo);
			}
			return result.ToArray();
		}
		bool IBundleQuery.ManifestValid()
		{
			return _activeManifest != null;
		}
		#endregion
	}
}