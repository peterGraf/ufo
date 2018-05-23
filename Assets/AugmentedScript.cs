/*
AugmentedScript.cs - MonoBehaviour for ufo - a Vuforia based location based AR-App.

Copyright (C) 2018   Tamiko Thiel and Peter Graf

This progam is derived from Matthew Hallberg`s GPS-Vuforia-Markerless-Project,
see https://github.com/MatthewHallberg/GPS-Vuforia-Markerless-Project.

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.

For more information on Tamiko Thiel or Peter Graf,
please see: http://www.mission-base.com/.
*/

using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.Networking;

public class AugmentedScript : MonoBehaviour
{
    private float _ufoLatitude = 0f;
    private float _ufoLongitude = 0f;
    private float _originalLatitude;
    private float _originalLongitude;
    private float _currentLongitude;
    private float _currentLatitude;
    private float _distance;
    private float _latDistance;
    private float _lonDistance;
    private float _heading;
    private float _headingShown;

    private GameObject _sceneAnchor;
    private GameObject _distanceTextObject;
    private string _locationError = null;

    private bool _setOriginalValues = true;

    private Vector3 _targetPosition;
    private Vector3 _originalPosition;

    private float _speed = .1f;

    private IEnumerator GetCoordinates()
    {
        while (string.IsNullOrEmpty(_locationError) )
        {
            // If original value has not yet been set save coordinates of player on app start
            if (_setOriginalValues)
            {
                _setOriginalValues = false;

                // Check if user has location service enabled
                if (!Input.location.isEnabledByUser)
                {
                    _locationError = "Please enable the location service.";
                    yield break;
                }

                // Enable the compass
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
                    _locationError = "Location service timed out.";
                    yield break;
                }

                // Connection has failed
                if (Input.location.status == LocationServiceStatus.Failed)
                {
                    _locationError = "Unable to determine device location";
                    yield break;
                }

                _originalLatitude = Input.location.lastData.latitude;
                _originalLongitude = Input.location.lastData.longitude;

                UnityWebRequest www = UnityWebRequest.Get("http://www.mission-base.com/ufo.txt");
                yield return www.SendWebRequest();

                if (www.isNetworkError || www.isHttpError)
                {
                    _locationError = www.error;
                    yield break;
                }

                // Get results as text
                var text = www.downloadHandler.text;
                if (string.IsNullOrEmpty(text))
                {
                    _locationError = "received empty text";
                    yield break;
                }

                // place the ufo
                var parts = text.Split(',');
                if (parts.Length != 3)
                {
                    _locationError = "bad text: " + text;
                    yield break;
                }
                double value;
                if (!double.TryParse(parts[1], out value))
                {
                    _locationError = "bad lat: " + parts[1];
                    yield break;
                }
                _ufoLatitude = (float)value;

                if (!double.TryParse(parts[2], out value))
                {
                    _locationError = "bad lon: " + parts[2];
                    yield break;
                }
                _ufoLongitude = (float)value;
            }

            // Overwrite current lat and lon everytime
            _currentLatitude = Input.location.lastData.latitude;
            _currentLongitude = Input.location.lastData.longitude;

            // Calculate the distance between where the player was when the app started and where they are now.
            //_distance = Calc(_originalLatitude, _originalLongitude, _currentLatitude, _currentLongitude);

            _latDistance = Calc(_ufoLatitude, _currentLongitude, _currentLatitude, _currentLongitude);
            _lonDistance = Calc(_currentLatitude, _ufoLongitude, _currentLatitude, _currentLongitude);

            _distance = Mathf.Sqrt(_latDistance * _latDistance + _lonDistance * _lonDistance);

            // Set the target position of the ufo, this is where we lerp to in the update function
            _targetPosition = new Vector3(0, _originalPosition.y, _distance);

            // Get the heading from the compass
            _heading = Input.compass.trueHeading;

            yield return null;
        }
    }

    // Calculates the distance between two sets of coordinates, taking into account the curvature of the earth.
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
        // Get references to objects
        _distanceTextObject = GameObject.FindGameObjectWithTag("distanceText");
        _sceneAnchor = GameObject.FindGameObjectWithTag("SceneAnchor");

        // Start GetCoordinate() function 
        StartCoroutine("GetCoordinates");

        // Initialize target and original position
        _targetPosition = transform.position;
        _originalPosition = transform.position;
    }

    void Update()
    {
        _headingShown += (float)((_heading - _headingShown) / 10.0);
        _sceneAnchor.transform.eulerAngles = new Vector3(0, 360 - _headingShown, 0);

        // Linearly interpolate from current position to target position
        transform.position = Vector3.Lerp(transform.position, _targetPosition, _speed);

        transform.eulerAngles = new Vector3(0, 360 - _headingShown, 0);

        // Set the distance text on the canvas
        if (!string.IsNullOrEmpty(_locationError))
        {
            _distanceTextObject.GetComponent<Text>().text = _locationError;
            return;
        }
        _distanceTextObject.GetComponent<Text>().text =
            "D " + _distance.ToString("F")
            + " Lat " + (_currentLatitude).ToString("F6")
            + " Lon " + (_currentLongitude).ToString("F6")
            + " H " + (Input.compass.enabled ? _headingShown.ToString("F") : "disabled");
    }
}
