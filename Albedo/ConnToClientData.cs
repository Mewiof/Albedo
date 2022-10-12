using System;
using System.Collections.Generic;
using System.Net;

namespace Albedo {

	public class ConnToClientData {

		public uint id;
		public EndPoint endPoint;
		// May be redundant, but still better than using '.ToString()' each time
		/// <summary>Cached 'endPoint.ToString()'</summary>
		public string address;

		/* 'Requested' state to make sure that client will not
		 * send multiple auth requests while waiting for server decision/response
		 */
		public enum AuthStage {
			NotAuthenticated,
			Requested,
			Authenticated
		}

		public AuthStage authStage;

		/* Server may need to call some functions with delay on main thread
		 * (e.g., to ~run timeout~ when authenticating or to give some time for
		 * disconn data to reach client before disconnecting it)
		 * 
		 * [!] Since 'ConnToClientData' is pooled and can be reassigned to another connection,
		 * it is important for us to be able to cancel all tasks before/on reassigning
		 */
		public class Task {

			public string name; // May be redundant
			public float timeLeft;
			public Action onCompleted;

			public void Set(string name, float timeLeft, Action onCompleted) {
				this.name = name; // May be redundant
				this.timeLeft = timeLeft;
				this.onCompleted = onCompleted;
			}
		}

		private readonly Pool<Task> _taskPool = new(() => new(), 2); // 2 should be enough for now
		private readonly List<Task> _activeTasks = new();
		private int _i;
		private Task _tempTask;

		/// <summary>Clear and assign</summary>
		public void Set(uint id, EndPoint endPoint) {
			// clear
			for (_i = 0; _i < _activeTasks.Count; _i++) {
				_taskPool.Return(_activeTasks[_i]);
			}
			_activeTasks.Clear();

			// assign
			this.id = id;
			this.endPoint = endPoint;
			address = endPoint.ToString();
			authStage = AuthStage.NotAuthenticated;
		}

		public void Tick(ref float delta) {
			for (_i = _activeTasks.Count - 1; _i >= 0; _i--) {
				_tempTask = _activeTasks[_i];
				_tempTask.timeLeft -= delta;
				if (_tempTask.timeLeft <= 0f) {
					// callback
					_tempTask.onCompleted.Invoke();
					// return to pool
					_taskPool.Return(_tempTask);
					_activeTasks.RemoveAt(_i);
				}
			}
		}

		public void AddTask(string name, float duration, Action onCompleted) {
			/* We could check if a task with
			 * this 'name' already exists, but I don't think it's worth CPU time
			 */
			_tempTask = _taskPool.Get();
			_tempTask.Set(name, duration, onCompleted);
			_activeTasks.Add(_tempTask);
		}

		/* At this point I don't see any scenario where we
		 * need to use the 'CancelTask' func or 'name' parameter since all active tasks are
		 * automatically cancelled when 'ConnToClientData' is reassigned
		 */

		// May be redundant
		public void CancelTask(string name) {
			for (_i = _activeTasks.Count - 1; _i >= 0; _i--) {
				_tempTask = _activeTasks[_i];
				if (_tempTask.name.Equals(name)) {
					// return to pool
					_taskPool.Return(_tempTask);
					_activeTasks.RemoveAt(_i);
				}
			}
		}
	}
}
