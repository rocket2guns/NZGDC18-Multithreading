using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Assets.Scripts
{

    [Serializable]
    public class WorkTask
    {
        /// <summary>
        /// Maximum number to be considered for generating a prime
        /// </summary>
        public static int MaximumNumber = 10000000;

        /// <summary>
        /// Random instance that we can use on the thread (can't use UnitySystem.Random)
        /// </summary>
        public static System.Random Randomized = new System.Random();

        public String Name;
        public int NumberToCheck;
        public bool IsPrime;

        public delegate void Event();

        /// <summary>
        /// Event subscribers to be run when work is completed and executed on main thread
        /// </summary>
        public event Event OnWorkComplete;

        /// <summary>
        /// Run on the main thread when collected from finished queue
        /// </summary>
        public void ExecuteOnMainThread()
        {
            if (OnWorkComplete != null) OnWorkComplete();
        }

        /// <summary>
        /// The method we run to get and determine a prime number
        /// </summary>
        public virtual void DoWork()
        {
            while (!IsPrime)
            {
                NumberToCheck = Randomized.Next(2, MaximumNumber);

                var failed = false;
                var holder = NumberToCheck;
                while (--holder > 1)
                {
                    if (NumberToCheck % holder == 0)
                    {
                        failed = true;
                        break;
                    }
                }
                IsPrime = !failed;
            }

            Name = NumberToCheck.ToString();
        }
    }

    [Serializable]
    public class ContainsZeroWorkTask : WorkTask
    {
        public bool ContainsAZero
        {
            get { return Name.Contains("0"); }
        }

        public override void DoWork()
        {
            base.DoWork();
            while (!ContainsAZero)
            {
                IsPrime = false;
                base.DoWork();
            }
        }
    }

    public class MultithreadedExample : MonoBehaviour
    {
        public Thread WorkThreadInstance;

        /// <summary>
        /// Thread will be ended when this is false
        /// </summary>
        public bool IsRunning;

        /// <summary>
        /// The pending queue used by the worker thread
        /// </summary>
        public Queue<WorkTask> PendingTasks = new Queue<WorkTask>();

        /// <summary>
        /// Finished tasks are added to this queue
        /// </summary>
        public Queue<WorkTask> FinishedTasks = new Queue<WorkTask>();

        /// <summary>
        /// The number of tasks completed
        /// </summary>
        public int TasksCompleted;

        void Awake()
        {
            IsRunning = true;
            WorkThreadInstance = new Thread(ThreadedWork);
            WorkThreadInstance.Start();
        }

        public void ThreadedWork()
        {
            // here is where our work will go
            while (IsRunning)
            {
                if (PendingTasks.Count > 0)
                {
                    TasksCompleted++;
                    var currentTask = PendingTasks.Dequeue();
                    currentTask.DoWork();
                    FinishedTasks.Enqueue(currentTask);
                    Thread.MemoryBarrier();
                }
                Thread.Sleep(1);
            }
        }

        void OnDestroy()
        {
            WorkThreadInstance.Abort();
            IsRunning = false;
        }

        public void CreateTask()
        {
            var newWorkTask = new ContainsZeroWorkTask();
            newWorkTask.OnWorkComplete += () => Debug.Log(newWorkTask.Name);
            PendingTasks.Enqueue(newWorkTask);
        }

        void Update()
        {
            if (FinishedTasks.Count == 0) return;
            while (FinishedTasks.Count > 0)
            {
                Thread.MemoryBarrier();
                var currentTask = FinishedTasks.Dequeue();
                currentTask.ExecuteOnMainThread();
            }
        }
    }


#if UNITY_EDITOR

    #region Editor Script

    [CustomEditor(typeof(MultithreadedExample), true)]
    public class MultithreadedExampleEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            if (EditorApplication.isPlaying)
            {
                // this will only run while the editor is playing
                CustomInspector();
            }
            base.OnInspectorGUI();
        }

        public virtual void CustomInspector()
        {
            var multithreadedExample = (MultithreadedExample)target;

            if (GUILayout.Button("Generate Prime"))
            {
                multithreadedExample.CreateTask();
            }
        }
    }

    #endregion

#endif
}
