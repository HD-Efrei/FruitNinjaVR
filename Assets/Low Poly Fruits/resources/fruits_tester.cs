using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class fruits_tester : MonoBehaviour {

	public GameObject[] fruits;
	// Use this for initialization
	void Start () 
	{
		
	}
	
	// Update is called once per frame
	void Update () 
	{
		
		// L'ancien RotateAround à deux arguments utilisait des radians.
		float angle = Time.deltaTime * Mathf.Rad2Deg;
		fruits[0].transform.Rotate(Vector3.up, angle, Space.World);
		fruits[1].transform.Rotate(Vector3.up, -angle, Space.World);
		fruits[2].transform.Rotate(Vector3.up, angle, Space.World);
		fruits[3].transform.Rotate(Vector3.up, -angle, Space.World);
		fruits[4].transform.Rotate(Vector3.up, angle, Space.World);
		fruits[5].transform.Rotate(Vector3.up, -angle, Space.World);
		fruits[6].transform.Rotate(Vector3.up, angle, Space.World);
		fruits[7].transform.Rotate(Vector3.up, -angle, Space.World);
		fruits[8].transform.Rotate(Vector3.up, angle, Space.World);
		fruits[9].transform.Rotate(Vector3.up, -angle, Space.World);
	
	}
}
