using UnityEngine;

public class CubeScript : MonoBehaviour {

	// Use this for initialization
	void Start () {
	}
	
	// Update is called once per frame
	void Update () {
        if ("SpinY".Equals(gameObject.name))
        {
            //rotate by 1 degree about the y axis every frame
            transform.eulerAngles += new Vector3(0, 1f, 0);
        }
        else if ("SpinX".Equals(gameObject.name))
        {
            //rotate by 1 degree about the x axis every frame
            transform.eulerAngles += new Vector3(1f, 0, 0);
        }
        else if ("SpinZ".Equals(gameObject.name))
        {
            //rotate by 1 degree about the z axis every frame
            transform.eulerAngles += new Vector3(0, 0, 1f);
        }
    }
}
