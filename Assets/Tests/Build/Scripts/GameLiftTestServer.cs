using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Aws.GameLift.Server;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class GameLiftTestServer : MonoBehaviour {
    private Queue<Action> _mainQueue = new Queue<Action>();
    
    private void Awake() {
        var initSdkOutcome = GameLiftServerAPI.InitSDK();
        if (!initSdkOutcome.Success)
        {
            Debug.LogError(initSdkOutcome);
            return;
        }

        var parameters = new ProcessParameters {
            Port = 8080,
            LogParameters = new LogParameters(new List<string>()),
            OnHealthCheck = () => true,
            OnStartGameSession = session => {
                Debug.Log(session.ToString());
                _mainQueue.Enqueue(() => StartCoroutine(EndMatch()));
                GameLiftServerAPI.ActivateGameSession();
            },
            OnUpdateGameSession = session => {
                Debug.Log(session.ToString());
            },
            OnProcessTerminate = () =>
            {
                GameLiftServerAPI.ProcessEnding();
            }
        };

        var processReadyOutcome = GameLiftServerAPI.ProcessReady(parameters);
        if (!processReadyOutcome.Success)
        {
            Debug.LogError(processReadyOutcome);
        }
        /*var info = new ProcessStartInfo
        {
            FileName = "/usr/local/Cellar/openjdk/15.0.2/libexec/openjdk.jdk/Contents/Home/bin/javac",
            Arguments = " -jar " + GetGameLiftLocalPath() + " -p 9080",
            UseShellExecute = false
        };
        info.RedirectStandardOutput = true;
        info.RedirectStandardError = true;
        info.CreateNoWindow = false;
        info.StandardErrorEncoding = Encoding.UTF8;
        info.StandardErrorEncoding = Encoding.UTF8;
        _process = Process.Start(info);
        if (_process == null) return;
        _process.OutputDataReceived += (sender, e) => {
            Debug.Log(e);
        };
        _process.ErrorDataReceived += (sender, e) => {
            Debug.LogError(e);
        };*/
    }

    private void Update()
    {
        if(_mainQueue.Count <= 0) return;
        var action = _mainQueue.Dequeue();
        action?.Invoke();
    }
    
    private IEnumerator EndMatch()
    {
        yield return new WaitForSeconds(3);
        GameLiftServerAPI.ProcessEnding();
        _mainQueue.Enqueue(Quit);
    }

    private void Quit() {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.ExitPlaymode();
#else
        Application.Quit();
#endif
    }

    private void OnDestroy()
    {
        GameLiftServerAPI.Destroy();
    }

    private string GetGameLiftLocalPath()
    {
        return Path.Combine(Application.dataPath, "Tests/GameLiftLocal/GameLiftLocal.jar");
    }
}
