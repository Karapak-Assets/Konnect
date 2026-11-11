using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Properties;
using UnityEngine;


namespace Karapak.Konnect
{
    public abstract class SessionBrowser : MonoBehaviour
    {
        public struct SearchContext
        {
            internal int SearchId;
            internal int ResultsFound;
            internal CancellationTokenSource CancelSource;
            internal Task SearchTask;
            public bool Cancelled => (CancelSource?.IsCancellationRequested ?? false) || (SearchTask?.IsCanceled ?? false);
            public bool Running => !Cancelled && !(SearchTask?.IsCompleted ?? false);

            internal void Cancel() => CancelSource?.Cancel();
            internal void Dispose()
            {
                CancelSource.Dispose();
            }
        }


        [SerializeField] private bool deferredClear;

        public event Action SearchStart; // whenever addresult called or addresults called (addresults for a batch so we dont spam this event)
        public event Action SearchUpdate; // whenever addresult called or addresults called (addresults for a batch so we dont spam this event)
        public event Action SearchEnd; // whenever addresult called or addresults called (addresults for a batch so we dont spam this event)
        public event Action<int> OnResultChange;

        private SearchContext currentSearch;
        private List<SessionBrowserResult> results = new List<SessionBrowserResult>();
        public IReadOnlyList<SessionBrowserResult> Results => results.AsReadOnly();

        private int LatestSearchId = 0;


        #region Unity Messages

        protected virtual void OnDestroy()
        {
            CancelSearch();
            ClearResults(LatestSearchId);
        }

        #endregion

        public async Task<bool> Search()
        {
            CancelSearch();

            int id = LatestSearchId;

            var ctx = new SearchContext()
            {
                SearchId = id,
                ResultsFound = 0,
                CancelSource = new CancellationTokenSource(),
                SearchTask = null,
            };
            currentSearch = ctx;

            SearchStart?.Invoke();

            if (!deferredClear)
                ClearResults(id, true);

            try
            {
                await (currentSearch.SearchTask = ExecuteSearch(id, ctx.CancelSource.Token));

                if (currentSearch.ResultsFound == 0)
                    ClearResults(id, true);

                SearchEnd?.Invoke();
            }
            catch (OperationCanceledException e)
            {
                // search was cancelled
                string log = Log($"Cancelled search {id}", $"{e.Message}");
                Debug.LogWarning(log);

                ctx.Dispose();

                SearchEnd?.Invoke();
                return false;
            }

            return true;
        }

        public void CancelSearch()
        {
            if (currentSearch.Running)
            {
                currentSearch.Cancel();

                //currentSearch = null;
            }
            UpdateSearchId();
        }

        private void UpdateSearchId()
        {
            LatestSearchId += 69; // ;)
        }

        protected void AddResult(int searchId, SessionBrowserResult result, bool pushUpdateEvent = false)
        {
            if (searchId != currentSearch.SearchId) return;

            if (deferredClear && currentSearch.ResultsFound == 0) // on first result of a new search we deferred clear
                results.Clear();

            ++currentSearch.ResultsFound;

            results.Add(result);

            int index = results.Count - 1; // local var to capture in lambda
            result.OnDataChanged += () => { OnResultChange?.Invoke(index); };

            if (pushUpdateEvent) PushUpdateEvent();
        }

        protected void ClearResults(int searchId, bool pushUpdateEvent = false)
        {
            if (searchId != LatestSearchId) return;

            foreach (var result in results)
            {
                result.Dispose(); // is this correct place?
            }
            results.Clear();

            if (pushUpdateEvent) PushUpdateEvent();

        }

        protected void PushUpdateEvent()
        {
            SearchUpdate?.Invoke();
        }

        protected abstract Task ExecuteSearch(int searchId, CancellationToken cancelled);

        private string Log(string title, string data) => Log<SessionBrowser>(title, data);
        private string Log<T>(string title, string data)
        {
            return $"[{nameof(T)}]{title}: {data}";
        }
    }

    public class SessionBrowserResult : IDisposable
    {
        // connection info (required to join it)
        public readonly string Connect;

        public readonly string Handler;
        public bool DataFault { get; private set; }

        // common data (aesthetic only but useful for ui)
        [CreateProperty]
        public Texture2D Icon { get; protected set; }

        [CreateProperty]
        public string Name { get; protected set; }

        [CreateProperty]
        public int Players { get; protected set; }

        [CreateProperty]
        public int Capacity { get; protected set; }

        [CreateProperty]
        public bool PasswordRequired { get; protected set; }

        [CreateProperty]
        public int? Ping { get; protected set; }


        // aux data keys / tags (third party support)
        private Dictionary<string, string> sessionData = new Dictionary<string, string>();

        // invoke when data loads
        public event Action OnDataChanged;

        // kill this result if load failure?


        public SessionBrowserResult(string connect, string handler)
        {
            Connect = connect;
            Handler = handler;
            DataFault = false;
        }

        public string GetTag(string key, string defaultValue = default)
        {
            if (key == null) return defaultValue;
            return sessionData.GetValueOrDefault(key, defaultValue);
        }

        public bool SetTag(string key, string value)
        {
            return sessionData.TryAdd(key, value);
        }

        protected void NotifyDataChange()
        {
            OnDataChanged?.Invoke();
        }

        protected void NotifyLoadFault()
        {
            DataFault = true;
            NotifyDataChange();
        }

        public virtual void Dispose()
        {

        }
    }

}