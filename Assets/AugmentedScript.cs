/*
AugmentedScript.cs - MonoBehaviour for arvosVun - a Vuforia/Unity based location based AR-App.

Copyright (C) 2018, Tamiko Thiel and Peter Graf

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

For more information on ARVOS,
please see: http://www.arvos-app.com/.

For more information on Tamiko Thiel or Peter Graf,
please see: http://www.mission-base.com/.
*/

using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections.Generic;

public class ArObject
{
    public GameObject GameObject;
    public string Text;
    public float Latitude;
    public float Longitude;
    public Vector3 TargetPosition;
}

public class AugmentedScript : MonoBehaviour
{
    private float _currentLongitude;
    private float _currentLatitude;
    private float _currentHeading;
    private float _originalLatitude;
    private float _originalLongitude;
    private float _headingShown;

    private GameObject _sceneAnchor;
    private GameObject _infoTextObject;
    private GameObject _wrapper;

    private string _locationError = null;

    private bool _setOriginalValues = true;

    private const float _speed = .1f;

    private List<ArObject> _arObjects = new List<ArObject>();

    // A Coroutine retrieving the current location and heading
    private IEnumerator GetCoordinates()
    {
        while (string.IsNullOrEmpty(_locationError))
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
                int maxWait = 30;
                while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
                {
                    yield return new WaitForSeconds(1);
                    maxWait--;
                }

                // Service didn't initialize in 30 seconds
                if (maxWait < 1)
                {
                    _locationError = "Location service timed out.";
                    yield break;
                }

                // Connection has failed
                if (Input.location.status == LocationServiceStatus.Failed)
                {
                    _locationError = "Unable to determine device location.";
                    yield break;
                }

                _originalLatitude = Input.location.lastData.latitude;
                _originalLongitude = Input.location.lastData.longitude;

                // Get the list of objects to show and their locations
                var url = "http://www.mission-base.com/ArvosVun.txt";
                UnityWebRequest www = UnityWebRequest.Get(url);
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
                    _locationError = "WebRequest to url '" + url + "' received empty text.";
                    yield break;
                }

                // Place the objects
                var lines = text.Split('\n');
                foreach (var line in lines)
                {
                    if (string.IsNullOrEmpty(line))
                    {
                        continue;
                    }

                    string arGameObjectName;
                    GameObject arGameObject;

                    var parts = line.Split(',');
                    if (parts.Length == 1)
                    {
                        // Destroy the objects, so they are not visible in the scene
                        arGameObjectName = parts[0];
                        arGameObject = GameObject.FindGameObjectWithTag(arGameObjectName);
                        if (arGameObject == null)
                        {
                            _locationError = "line '" + line + "', bad name: " + arGameObjectName;
                            yield break;
                        }
                        Destroy(arGameObject, .1f);
                        continue;
                    }

                    // 4 parts: Tag, Name to set, Lat, Lon
                    if (parts.Length != 4)
                    {
                        _locationError = "line '" + line + "', bad text: " + text;
                        yield break;
                    }

                    // First part is the tag of the game object
                    arGameObjectName = parts[0];
                    arGameObject = GameObject.FindGameObjectWithTag(arGameObjectName);
                    if (arGameObject == null)
                    {
                        _locationError = "line '" + line + "', bad name: " + arGameObjectName;
                        yield break;
                    }

                    // Wrap the object in a wrapper
                    var wrapper = Instantiate(_wrapper);
                    if (wrapper == null)
                    {
                        _locationError = "Instantiate(_wrapper) failed";
                        yield break;
                    }
                    wrapper.transform.parent = _sceneAnchor.transform;

                    // Create a copy of the object
                    arGameObject = Instantiate(arGameObject);
                    if (arGameObject == null)
                    {
                        _locationError = "Instantiate(gameObject) failed";
                        yield break;
                    }
                    arGameObject.transform.parent = wrapper.transform;
                    arGameObject.name = parts[1];

                    // Get lat and lon of the object
                    double value;
                    if (!double.TryParse(parts[2], out value))
                    {
                        _locationError = "line '" + line + "', bad lat: " + parts[2];
                        yield break;
                    }
                    var latitude = (float)value;

                    if (!double.TryParse(parts[3], out value))
                    {
                        _locationError = "line '" + line + "', bad lon: " + parts[3];
                        yield break;
                    }
                    var longitude = (float)value;

                    // Create the ar object
                    var arObject = new ArObject { Text = line, GameObject = wrapper, Latitude = latitude, Longitude = longitude };
                    _arObjects.Add(arObject);
                }

                if (!string.IsNullOrEmpty(_locationError))
                {
                    yield break;
                }
            }

            // Overwrite current lat and lon everytime
            _currentLatitude = Input.location.lastData.latitude;
            _currentLongitude = Input.location.lastData.longitude;

            // Get the heading from the compass
            _currentHeading = Input.compass.trueHeading;

            // Calculate positions for all ar objects
            foreach (var arObject in _arObjects)
            {
                var latDistance = Calc(arObject.Latitude, _currentLongitude, _currentLatitude, _currentLongitude);
                var lonDistance = Calc(_currentLatitude, arObject.Longitude, _currentLatitude, _currentLongitude);

                var distance = Mathf.Sqrt(latDistance * latDistance + lonDistance * lonDistance);

                // Set the target position of the object, this is where we lerp to in update
                arObject.TargetPosition = new Vector3(0, arObject.GameObject.transform.position.y, distance);
            }
            yield return null;
        }
    }

    // Calculates the distance between two sets of coordinates, taking into account the curvature of the earth
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
        _infoTextObject = GameObject.FindGameObjectWithTag("distanceText");
        if (_infoTextObject == null)
        {
            _locationError = "Cannot find object with tag distanceText";
        }

        _sceneAnchor = GameObject.FindGameObjectWithTag("SceneAnchor");
        if (_sceneAnchor == null)
        {
            _locationError = "Cannot find object with tag SceneAnchor";
        }

        _wrapper = GameObject.FindGameObjectWithTag("Wrapper");
        if (_wrapper == null)
        {
            _locationError = "Cannot find object with tag Wrapper";
        }

        // Start GetCoordinate() function 
        StartCoroutine("GetCoordinates");
    }

    void Update()
    {
        // Set any error text on the canvas
        if (!string.IsNullOrEmpty(_locationError))
        {
            _infoTextObject.GetComponent<Text>().text = _locationError;
            return;
        }

        // Calculate heading
        if (!((_currentHeading < 180 && _headingShown < 180) || (_currentHeading > 180 && _headingShown > 180)))
        {
            if (_currentHeading < 180)
            {
                _currentHeading += 360;
            }
            else
            {
                _headingShown += 360;
            }
        }
        _headingShown += (float)((_currentHeading - _headingShown) / 20.0);

        while (_headingShown > 360)
        {
            _headingShown -= 360;
        }

        _sceneAnchor.transform.eulerAngles = new Vector3(0, 360 - _headingShown, 0);

        // Place the ar objects
        foreach (var arObject in _arObjects)
        {
            // Linearly interpolate from current position to target position
            arObject.GameObject.transform.position = Vector3.Lerp(arObject.GameObject.transform.position, arObject.TargetPosition, _speed);
            arObject.GameObject.transform.eulerAngles = new Vector3(0, 360 - _headingShown, 0);
        }

        // Set info text
        var latDistance = Calc(_originalLatitude, _currentLongitude, _currentLatitude, _currentLongitude);
        var lonDistance = Calc(_currentLatitude, _originalLongitude, _currentLatitude, _currentLongitude);

        var distance = Mathf.Sqrt(latDistance * latDistance + lonDistance * lonDistance);

        _infoTextObject.GetComponent<Text>().text =
            "D " + distance.ToString("F")
            + " N " + _arObjects.Count
            + " Lat " + (_currentLatitude).ToString("F6")
            + " Lon " + (_currentLongitude).ToString("F6")
            + " H " + (Input.compass.enabled ? _headingShown.ToString("F") : "disabled");
    }
}
