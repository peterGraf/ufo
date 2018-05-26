using UnityEngine;

public class RedCube : MonoBehaviour {

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
        if ("RC1".Equals(gameObject.name))
        {
            //rotate by 1 degree about the y axis every frame
            transform.eulerAngles += new Vector3(0, 1f, 0);
        }
    }
}
