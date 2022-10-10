using System;
using System.Collections.Generic;

namespace Albedo {

	public class TaskManager<TKey> {

		public class Task {

			public TKey key;
			public float timeLeft;
			public Action onCompleted;

			public void Set(float timeLeft, Action onCompleted) {
				this.timeLeft = timeLeft;
				this.onCompleted = onCompleted;
			}
		}

		private readonly Pool<Task> _taskPool = new(() => new(), 1000);
		private readonly List<Task> _activeTasks = new();
		private readonly List<Task> _removeList = new();
		private int i;
		private Task _tempTask;

		public void Add(float duration, Action onCompleted) {
			_tempTask = _taskPool.Get();
			_tempTask.Set(duration, onCompleted);
			_activeTasks.Add(_tempTask);
		}

		public Task Get(TKey key) {
			for (i = 0; i < _activeTasks.Count; i++) {
				if (_activeTasks[i].key.Equals(key)) {
					return _activeTasks[i];
				}
			}
			return null;
		}

		public void Tick(float delta) {
			_removeList.Clear();
			for (i = 0; i < _activeTasks.Count; i++) {
				_tempTask = _activeTasks[i];
				_tempTask.timeLeft -= delta;
				if (_tempTask.timeLeft <= 0f) {
					_tempTask.onCompleted.Invoke();
					_removeList.Add(_tempTask);
				}
			}
			for (i = 0; i < _removeList.Count; i++) {
				_tempTask = _removeList[i];
				_ = _activeTasks.Remove(_tempTask);
				_taskPool.Return(_tempTask);
			}
		}

		public void Remove(Task task) {
			_ = _activeTasks.Remove(task);
		}
	}
}
