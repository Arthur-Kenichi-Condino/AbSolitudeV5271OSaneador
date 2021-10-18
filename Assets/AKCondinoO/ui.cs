using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
namespace AKCondinoO{internal class ui:MonoBehaviour{
public void OnBtnHostClick(){
NetworkManager.Singleton.StartHost();
}
[SerializeField]Text consolePlaceholder;
float frameTimeDelta;float millisecondsPerFrame;float fps;float fpsTextRefreshTimer;float fpsTextRefreshTime=.5f;
void Update(){
frameTimeDelta+=Time.deltaTime-frameTimeDelta;millisecondsPerFrame=frameTimeDelta*1000.0f;fps=1.0f/frameTimeDelta;
fpsTextRefreshTimer+=Time.deltaTime;
if(fpsTextRefreshTimer>=fpsTextRefreshTime){fpsTextRefreshTimer=0;
consolePlaceholder.text=string.Format("FPS:{0}",fps);
}
}
}}