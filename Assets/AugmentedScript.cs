using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class AugmentedScript : MonoBehaviour
{
    private float _originalLatitude;
    private float _originalLongitude;
    private float _currentLongitude;
    private float _currentLatitude;
    private float _distance;
    private float _heading;

    private GameObject _distanceTextObject;

    private bool _setOriginalValues = true;

    private Vector3 _targetPosition;
    private Vector3 _originalPosition;

    private float _speed = .1f;

    private IEnumerator GetCoordinates()
    {
        //while true so this function keeps running once started.
        while (true)
        {
            // check if user has location service enabled
            if (!Input.location.isEnabledByUser)
            {
                _distanceTextObject.GetComponent<Text>().text =
                    "Please enable location services.";
                yield break;
            }

            // enable compass
            Input.compass.enabled = true;

            // Start service before querying location
            Input.location.Start(1f, .1f);

            // Wait until service initializes
            int maxWait = 20;
            while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
            {
                yield return new WaitForSeconds(1);
                maxWait--;
            }

            // Service didn't initialize in 20 seconds
            if (maxWait < 1)
            {
                _distanceTextObject.GetComponent<Text>().text =
                    "Location services timed out.";
                yield break;
            }

            // Connection has failed
            if (Input.location.status == LocationServiceStatus.Failed)
            {
                _distanceTextObject.GetComponent<Text>().text = 
                    "Unable to determine device location";
                yield break;
            }
            else
            {
                //if original value has not yet been set save coordinates of player on app start
                if (_setOriginalValues)
                {
                    _originalLatitude = Input.location.lastData.latitude;
                    _originalLongitude = Input.location.lastData.longitude;
                    _setOriginalValues = false;
                }

                //overwrite current lat and lon everytime
                _currentLatitude = Input.location.lastData.latitude;
                _currentLongitude = Input.location.lastData.longitude;

                //calculate the distance between where the player was when the app started and where they are now.
                _distance = Calc(_originalLatitude, _originalLongitude, _currentLatitude, _currentLongitude);

                //set the target position of the ufo, this is where we lerp to in the update function
                _targetPosition = _originalPosition - new Vector3(0, 0, _distance * 12);
                //distance was multiplied by 12 so I didn't have to walk that far to get the UFO to show up closer

                _heading = Input.compass.trueHeading;
            }
            Input.location.Stop();
        }
    }

    //calculates distance between two sets of coordinates, taking into account the curvature of the earth.
    private float Calc(float lat1, float lon1, float lat2, float lon2)
    {
        var R = 6378.137; // Radius of earth in KM
        var dLat = lat2 * Mathf.PI / 180 - lat1 * Mathf.PI / 180;
        var dLon = lon2 * Mathf.PI / 180 - lon1 * Mathf.PI / 180;
        float a = Mathf.Sin(dLat / 2) * Mathf.Sin(dLat / 2) +
          Mathf.Cos(lat1 * Mathf.PI / 180) * Mathf.Cos(lat2 * Mathf.PI / 180) *
          Mathf.Sin(dLon / 2) * Mathf.Sin(dLon / 2);
        var c = 2 * Mathf.Atan2(Mathf.Sqrt(a), Mathf.Sqrt(1 - a));
        var distance = R * c;
        return (float)(distance * 1000f); // meters
    }

    void Start()
    {
        //get distance text reference
        _distanceTextObject = GameObject.FindGameObjectWithTag("distanceText");
        //start GetCoordinate() function 
        StartCoroutine("GetCoordinates");
        //initialize target and original position
        _targetPosition = transform.position;
        _originalPosition = transform.position;
    }

    void Update()
    {
        //linearly interpolate from current position to target position
        transform.position = Vector3.Lerp(transform.position, _targetPosition, _speed);
        //rotate by 1 degree about the y axis every frame
        transform.eulerAngles += new Vector3(0, 1f, 0);

        //set the distance text on the canvas
        _distanceTextObject.GetComponent<Text>().text =
            "D " + _distance.ToString("F")
            + " Lat " + _currentLatitude.ToString("F6")
            + " Lon " + _currentLongitude.ToString("F6")
            + " H " + (Input.compass.enabled ? _heading.ToString("F") : "disabled");
    }
}
