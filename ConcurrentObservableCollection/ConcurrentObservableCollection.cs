using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading;

// ReSharper disable once CheckNamespace
namespace ObservableCollectionsEx
{
    public class ConcurrentObservableCollection<T> : IReadOnlyList<T>, INotifyCollectionChanged, INotifyPropertyChanged
    {
        #region Fields

        private readonly List<T> m_list;
        private readonly ConcurrentQueue<(DateTime time, T item)> m_buffer = new ConcurrentQueue<(DateTime, T)>();
        private TimeSpan m_period;
        private readonly Timer m_timer;
        private readonly TimeSpan m_expirationThreshold;

        #endregion

        #region Properties

        public int Count => m_list.Count;

        private TimeSpan Period
        {
            get => m_period;
            set
            {
                m_period = value;
                m_timer.Change(value, value);
            }
        }

        #endregion

        #region Constructors

        public ConcurrentObservableCollection(int delay)
        {
            m_expirationThreshold = TimeSpan.FromMilliseconds(delay * 10);
            m_list = new List<T>();
            m_period = Timeout.InfiniteTimeSpan;
            m_timer = new Timer(_ => ProcessQueue(), null, m_period, m_period);
        }

        public ConcurrentObservableCollection()
            : this(10)
        {
        }

        public ConcurrentObservableCollection(IEnumerable<T> collection) => m_list = new List<T>(collection);

        #endregion

        #region Events

        /// <inheritdoc />
        public event NotifyCollectionChangedEventHandler CollectionChanged;

        /// <inheritdoc />
        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region Methods

        /// <summary>
        /// Raises a PropertyChanged event (per <see cref="INotifyPropertyChanged" />).
        /// </summary>
        protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
            => PropertyChanged?.Invoke(this, e);

        /// <summary>
        /// Helper to raise a PropertyChanged event  />).
        /// </summary>
        private void OnPropertyChanged(string propertyName)
            => OnPropertyChanged(new PropertyChangedEventArgs(propertyName));

        protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
            => CollectionChanged?.Invoke(this, e);

        /// <summary>
        /// Helper to raise CollectionChanged event to any listeners
        /// </summary>
        private void OnCollectionChanged(NotifyCollectionChangedAction action, object item, int index)
            => OnCollectionChanged(new NotifyCollectionChangedEventArgs(action, item, index));

        /// <summary>
        /// Helper to raise CollectionChanged event with action == Reset to any listeners
        /// </summary>
        private void OnCollectionReset()
        {
            OnPropertyChanged("Item[]");
            OnPropertyChanged(nameof(Count));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        private void ProcessQueue()
        {
            var now = DateTime.Now;
            var max = now.AddMilliseconds(-m_expirationThreshold.Milliseconds - 5);

            while (m_buffer.TryPeek(out var peakValue) && DateTime.Compare(peakValue.time, max) < 0)
                if (m_buffer.TryDequeue(out var value))
                {
                    m_list.Add(value.item);

                    OnPropertyChanged("Item[]");
                    OnPropertyChanged(nameof(Count));
                    OnCollectionChanged(NotifyCollectionChangedAction.Add, value.item, m_list.Count - 1);
                }
        }

        private void QueueTimer()
        {
            if (Period == Timeout.InfiniteTimeSpan)
                Period = m_expirationThreshold;
        }

        private void QueueNewEntry(T item)
        {
            m_buffer.Enqueue((DateTime.Now, item));
            QueueTimer();
        }

        public IEnumerator<T> GetEnumerator()
            => m_list.GetEnumerator();

        public void Add(T item)
            => QueueNewEntry(item);

        public void Clear()
        {
            m_list.Clear();
            OnCollectionReset();
        }

        public bool Contains(T item)
            => m_list.Contains(item);

        public bool Remove(T item)
        {
            OnPropertyChanged("Item[]");
            OnPropertyChanged(nameof(Count));
            return m_list.Remove(item);
        }

        public int IndexOf(T item)
            => m_list.IndexOf(item);

        public T this[int index]
        {
            get => m_list[index];
            set => m_list[index] = value;
        }

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();

        #endregion
    }
}
