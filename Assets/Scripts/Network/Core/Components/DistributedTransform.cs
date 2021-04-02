/**
 * Copyright (c) 2019 LG Electronics, Inc.
 *
 * This software contains code licensed as described in LICENSE.
 *
 */

namespace Simulator.Network.Core.Components
{
	using System;
	using System.Collections;

	using Messaging.Data;
	using Threading;
	using UnityEngine;

	/// <summary>
	/// Distributed transform component
	/// </summary>
	public class DistributedTransform : DistributedComponent
	{
		/// <summary>
		/// Snapshot data being sent over the network
		/// </summary>
		private struct SnapshotData
		{
			public Vector3 LocalPosition;

			public Quaternion LocalRotation;

			public Vector3 LocalScale;
		}

		/// <summary>
		/// Maximum snapshots sent per second
		/// </summary>
		public int SnapshotsPerSecondLimit { get; set; } = 60;

		/// <summary>
		/// Time when the last snapshot has been sent
		/// </summary>
		private float lastSentSnapshotTime = float.MinValue;

		/// <summary>
		/// Timestamp of the last received snapshot
		/// </summary>
		private DateTime lastReceivedSnapshotTimestamp;

		/// <inheritdoc/>
		protected override string ComponentKey { get; } = "DistributedTransform";

		/// <inheritdoc/>
		protected override bool DestroyWithoutParent { get; } = true;

		/// <inheritdoc/>
		protected override int SnapshotMaxSize { get; } =
			ByteCompression.PositionRequiredBytes + ByteCompression.RotationMaxRequiredBytes;

		/// <summary>
		/// Last snapshot data which has been sent
		/// </summary>
		private SnapshotData lastSentSnapshot;

		/// <summary>
		/// Coroutine of the UpdateSnapshots method
		/// </summary>
		private IEnumerator updateSnapshotsCoroutine;

		/// <summary>
		/// Is the update snapshots coroutine running on the dispatcher
		/// </summary>
		private bool coroutineOnDispatcher;

		/// <inheritdoc/>
		public override void Initialize()
		{
			base.Initialize();
			
			ParentObject.IsAuthoritativeChanged += ParentObjectOnIsAuthoritativeChanged;
			ParentObjectOnIsAuthoritativeChanged(ParentObject.IsAuthoritative);
		}

		/// <inheritdoc/>
		public override void Deinitialize()
		{
			if (this == null)
				return;
			//Check if this object is currently being destroyed
			base.Deinitialize();
			ParentObject.IsAuthoritativeChanged -= ParentObjectOnIsAuthoritativeChanged;
			
			if (updateSnapshotsCoroutine == null)
				return;
			if (coroutineOnDispatcher)
			{
				ThreadingUtilities.Dispatcher.StopCoroutine(updateSnapshotsCoroutine);
				updateSnapshotsCoroutine = null;
			}
			else
			{
				StopCoroutine(updateSnapshotsCoroutine);
				updateSnapshotsCoroutine = null;
			}
		}

		/// <summary>
		/// Method that starts or stops required coroutines
		/// </summary>
		/// <param name="isAuthoritative">Is the parent <see cref="DistributedObject"/> authoritative</param>
		private void ParentObjectOnIsAuthoritativeChanged(bool isAuthoritative)
		{
			if (isAuthoritative)
			{
				if (updateSnapshotsCoroutine != null) 
					return;

				updateSnapshotsCoroutine = UpdateSnapshots();
				if (isActiveAndEnabled)
				{
					StartCoroutine(updateSnapshotsCoroutine);
					coroutineOnDispatcher = false;
				}
				else
				{
					ThreadingUtilities.Dispatcher.StartCoroutine(updateSnapshotsCoroutine);
					coroutineOnDispatcher = true;
				}
			}
			else
			{
				if (updateSnapshotsCoroutine == null)
					return;
			
				if (coroutineOnDispatcher)
				{
					ThreadingUtilities.Dispatcher.StopCoroutine(updateSnapshotsCoroutine);
					updateSnapshotsCoroutine = null;
				}
				else
				{
					StopCoroutine(updateSnapshotsCoroutine);
					updateSnapshotsCoroutine = null;
				}
			}
		}

		/// <summary>
		/// Unity OnEnable method
		/// </summary>
		protected void OnEnable()
		{
			if (ParentObject!=null && ParentObject.IsAuthoritative)
			{
				if (updateSnapshotsCoroutine != null) return;

				updateSnapshotsCoroutine = UpdateSnapshots();
				StartCoroutine(updateSnapshotsCoroutine);
				coroutineOnDispatcher = false;
			}
		}

		/// <summary>
		/// Unity OnDisable method
		/// </summary>
		protected void OnDisable()
		{
			if (!coroutineOnDispatcher)
				updateSnapshotsCoroutine = null;
		}

		/// <summary>
		/// Coroutine that sends updated snapshots if snapshot changes
		/// </summary>
		protected IEnumerator UpdateSnapshots()
		{
			BroadcastSnapshot(true);
			yield return null;
			while (IsInitialized)
			{
				if (Time.unscaledTime >= lastSentSnapshotTime + 1.0f / SnapshotsPerSecondLimit &&
				    AnySnapshotElementChanged(ByteCompression.PositionPrecision))
				{
					BroadcastSnapshot();
					lastSentSnapshotTime = Time.unscaledTime;
				}
				yield return null;
			}
			updateSnapshotsCoroutine = null;
		}

		/// <summary>
		/// Checks if any element of the snapshot has changed by more than given precision
		/// </summary>
		/// <param name="precision">Precision when comparing current value with previous</param>
		/// <returns>Has any of the snapshot elements changed</returns>
		private bool AnySnapshotElementChanged(float precision)
		{
			var thisTransform = transform;
			var localRotation = thisTransform.localRotation;
			var localPosition = thisTransform.localPosition;

			//TODO improve checking rotation changes
			return Mathf.Abs(lastSentSnapshot.LocalPosition.x - localPosition.x) > precision ||
			       Mathf.Abs(lastSentSnapshot.LocalPosition.y - localPosition.y) > precision ||
			       Mathf.Abs(lastSentSnapshot.LocalPosition.z - localPosition.z) > precision ||
			       Mathf.Abs(lastSentSnapshot.LocalRotation.x - localRotation.x) > precision ||
			       Mathf.Abs(lastSentSnapshot.LocalRotation.y - localRotation.y) > precision ||
			       Mathf.Abs(lastSentSnapshot.LocalRotation.z - localRotation.z) > precision ||
			       Mathf.Abs(lastSentSnapshot.LocalRotation.w - localRotation.w) > precision ||
			       ScaleChanged();
		}

		/// <summary>
		/// Checks if local scale of the snapshot has changed by epsilon
		/// </summary>
		/// <returns>Has local scale of the snapshot changed</returns>
		private bool ScaleChanged()
		{
			var precision = Mathf.Epsilon;
			var localScale = transform.localScale;
			return Mathf.Abs(lastSentSnapshot.LocalScale.x - localScale.x) > precision ||
			       Mathf.Abs(lastSentSnapshot.LocalScale.y - localScale.y) > precision ||
			       Mathf.Abs(lastSentSnapshot.LocalScale.z - localScale.z) > precision;
		}

		/// <inheritdoc/>
		protected override void PushSnapshot(BytesStack messageContent)
		{
			//Reverse order when writing to the stack
			var thisTransform = transform;
			var localRotation = thisTransform.localRotation;
			var localPosition = thisTransform.localPosition;
			var localScale = thisTransform.localScale;
			messageContent.PushUncompressedVector3(localScale);
			messageContent.PushCompressedRotation(localRotation);
			messageContent.PushCompressedPosition(localPosition);
			lastSentSnapshot.LocalRotation = localRotation;
			lastSentSnapshot.LocalPosition = localPosition;
			lastSentSnapshot.LocalScale = localScale;
		}

		/// <inheritdoc/>
		protected override void ApplySnapshot(DistributedMessage distributedMessage)
		{
			if (distributedMessage.ServerTimestamp <= lastReceivedSnapshotTimestamp) return;

			lastReceivedSnapshotTimestamp = distributedMessage.ServerTimestamp;

			//Parse incoming snapshot
			transform.localPosition = distributedMessage.Content.PopDecompressedPosition();
			transform.localRotation = distributedMessage.Content.PopDecompressedRotation();
			transform.localScale = distributedMessage.Content.PopUncompressedVector3();
		}
	}
}
