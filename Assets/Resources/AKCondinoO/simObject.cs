using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
namespace AKCondinoO{internal class simObject:NetworkBehaviour{
internal LinkedListNode<simObject>disabled;
internal ulong?id=null;internal(ulong id,int?cnkIdx)?fileIndex=null;
internal new Collider[]collider;internal new Rigidbody rigidbody;
internal new Renderer[]renderer;
void OnDisable(){//Debug.Log("simObject OnDisable");
}
void Awake(){Debug.Log("simObject Awake");
collider=GetComponentsInChildren<Collider>();rigidbody=GetComponent<Rigidbody>();
renderer=GetComponentsInChildren<Renderer>();
DisableSim();
}
void OnEnable(){//Debug.Log("simObject OnEnable");
}
internal bool isSimEnabled=true;
void DisableSim(){
if(isSimEnabled){Debug.Log("DisableSim");
isSimEnabled=false;
foreach(var col in collider){col.enabled=false;}if(rigidbody){rigidbody.constraints=RigidbodyConstraints.FreezeAll;}
foreach(var ren in renderer){ren.enabled=false;}
}
}
}}