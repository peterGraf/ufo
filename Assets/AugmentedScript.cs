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
using System;
using System.Linq;

public class ArObject
{
    public GameObject GameObject;
    public string Text;
    public float Latitude;
    public float Longitude;
    public float RelativeAltitude;
    public Vector3 TargetPosition;
    public bool IsRelative;
}

public class AugmentedScript : MonoBehaviour
{
    private float _currentLongitude = 0;
    private float _currentLatitude = 0;
    private float _currentHeading = 0;

    private float _originalLatitude = 0;
    private float _originalLongitude = 0;
    private float _headingShown = 0;

    private Transform _cameraTransform = null;
    private GameObject _sceneAnchor = null;
    private GameObject _infoText = null;
    private GameObject _wrapper = null;

    private bool _showInfo = false;
    private string _error = null;

    private bool _doInitialize = true;

    private List<ArObject> _arObjects = new List<ArObject>();

    private float _initialCameraAngle = 0;
    private float _initialHeading = 0;
    private long _startSecond = 0;
    private bool _cameraIsInitializing = true;

    // Create the ar objects depending on the downloaded text
    private void CreateArObjects(string text)
    {
        // Place the objects
        var lines = text.Split('\n');
        foreach (var entry in lines)
        {
            if (string.IsNullOrEmpty(entry))
            {
                // Empty line, ignore
                continue;
            }
            var line = entry.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("#") || line.StartsWith("//"))
            {
                // Empty line or comment line, ignore
                continue;
            }

            string arGameObjectTag;
            GameObject arGameObject = null;

            var parts = line.Split(',');
            if (parts.Length == 1)
            {
                if ("ShowInfo".Equals(parts[0].Trim()))
                {
                    _showInfo = true;
                    continue;
                }
                else
                {
                    _error = line + "', bad command: " + parts[0].Trim();
                    break;
                }
            }
            if (parts.Length == 2)
            {
                if ("DEL".Equals(parts[0].Trim()))
                {
                    // Destroy the objects, so they are not visible in the scene
                    arGameObjectTag = parts[1].Trim();
                    arGameObject = FindGameObjectWithTag(arGameObjectTag);
                    if (arGameObject == null)
                    {
                        _error = line + ", bad tag: " + arGameObjectTag;
                        break;
                    }
                    Destroy(arGameObject, .1f);
                    continue;
                }
                else
                {
                    _error = line + ", bad command: " + parts[0].Trim();
                    break;
                }
            }

            // 6 parts: Command,Tag, Name to set, Lat, Lon, alt
            if (parts.Length != 6)
            {
                _error = line + ", bad text: ";
                break;
            }

            if (!"REL".Equals(parts[0].Trim()) && !"ABS".Equals(parts[0].Trim()))
            {
                _error = line + ", bad command: " + parts[0].Trim();
                break;
            }

            // First part is the tag of the game object
            arGameObjectTag = parts[1].Trim();

            arGameObject = FindGameObjectWithTag(arGameObjectTag);
            if (arGameObject == null)
            {
                _error = line + ", bad tag: '" + arGameObjectTag + "'";
                break;
            }

            // Wrap the object in a wrapper
            var wrapper = Instantiate(_wrapper);
            if (wrapper == null)
            {
                _error = "Instantiate(_wrapper) failed";
                break;
            }
            wrapper.transform.parent = _sceneAnchor.transform;

            // Create a copy of the object
            arGameObject = Instantiate(arGameObject);
            if (arGameObject == null)
            {
                _error = "Instantiate(" + arGameObjectTag + ") failed";
                break;
            }
            arGameObject.transform.parent = wrapper.transform;

            // Set the name of the instantiated game object
            arGameObject.name = parts[2].Trim();

            if ("ABS".Equals(parts[0].Trim()))
            {
                // Get lat and lon of the object
                double value;
                if (!double.TryParse(parts[3].Trim(), out value))
                {
                    _error = line + ", bad lat: " + parts[3].Trim();
                    break;
                }
                var latitude = (float)value;

                if (!double.TryParse(parts[4].Trim(), out value))
                {
                    _error = line + ", bad lon: " + parts[4].Trim();
                    break;
                }
                var longitude = (float)value;

                if (!double.TryParse(parts[5].Trim(), out value))
                {
                    _error = line + ", bad alt: " + parts[5].Trim();
                    break;
                }
                var altitude = (float)value;

                // Create the AR object
                var arObject = new ArObject
                {
                    IsRelative = false,
                    Text = line,
                    GameObject = wrapper,
                    Latitude = latitude,
                    Longitude = longitude,
                    RelativeAltitude = altitude
                };

                // Latitude or longitude 0 means objects should take device latitude or longitude
                var arObjectLatitude = arObject.Latitude;
                if(arObjectLatitude == 0f)
                {
                    arObjectLatitude = _originalLatitude;
                }
                var arObjectLongitude = arObject.Longitude;
                if(arObjectLongitude == 0f)
                {
                    arObjectLongitude = _originalLongitude;
                }

                var latDistance = Calc(arObjectLatitude, arObjectLongitude, _originalLatitude, arObjectLongitude);
                var lonDistance = Calc(arObjectLatitude, arObjectLongitude, arObjectLatitude, _originalLongitude);

                var distance = Mathf.Sqrt(latDistance * latDistance + lonDistance * lonDistance);
                if (distance < 250)
                {
                    _arObjects.Add(arObject);
                }
            }
            else
            {
                // Get x offset and z offset of the object
                double value;
                if (!double.TryParse(parts[3].Trim(), out value))
                {
                    _error = line + ", bad x: " + parts[3].Trim();
                    break;
                }
                var xOffset = (float)value;

                if (!double.TryParse(parts[4].Trim(), out value))
                {
                    _error = line + ", bad z: " + parts[4].Trim();
                    break;
                }
                var zOffset = (float)value;

                if (!double.TryParse(parts[5].Trim(), out value))
                {
                    _error = line + ", bad alt: " + parts[5].Trim();
                    break;
                }
                var altitude = (float)value;

                // Create the ar object
                var arObject = new ArObject
                {
                    IsRelative = true,
                    Text = line,
                    GameObject = wrapper,
                    RelativeAltitude = altitude
                };
                _arObjects.Add(arObject);
                arObject.GameObject.transform.position = arObject.TargetPosition = new Vector3(xOffset, arObject.RelativeAltitude, zOffset);
            }
        }
    }

    // Calculate positions for all ar objects
    private void PlaceArObjects()
    {
        foreach (var arObject in _arObjects)
        {
            if (!arObject.IsRelative)
            {
                var latDistance = arObject.Latitude == 0F ? 0F : Calc(arObject.Latitude, arObject.Longitude, _currentLatitude, arObject.Longitude);
                var lonDistance = arObject.Longitude == 0F ? 0F : Calc(arObject.Latitude, arObject.Longitude, arObject.Latitude, _currentLongitude);

                if (arObject.Latitude < _currentLatitude)
                {
                    if (latDistance > 0)
                    {
                        latDistance *= -1;
                    }
                }
                else
                {
                    if (latDistance < 0)
                    {
                        latDistance *= -1;
                    }
                }
                if (arObject.Longitude < _currentLongitude)
                {
                    if (lonDistance > 0)
                    {
                        lonDistance *= -1;
                    }
                }
                else
                {
                    if (lonDistance < 0)
                    {
                        lonDistance *= -1;
                    }
                }
                // Set the target position of the object, this is where we lerp to in update
                arObject.TargetPosition = new Vector3(lonDistance, arObject.RelativeAltitude, latDistance);
            }
        }
    }

    // A Coroutine retrieving the object locations and the current location and heading
    private IEnumerator GetCoordinates()
    {
        while (string.IsNullOrEmpty(_error))
        {
            // On app start save location and heading and retrieve objects to show 
            if (_doInitialize)
            {
                _doInitialize = false;

                // Check if user has location service enabled
                if (!Input.location.isEnabledByUser)
                {
                    _error = "Please enable the location service.";
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
                    _error = "Location service timed out.";
                    yield break;
                }

                // Connection has failed
                if (Input.location.status == LocationServiceStatus.Failed)
                {
                    _error = "Unable to determine device location.";
                    yield break;
                }

                // Read location
                _originalLatitude = Input.location.lastData.latitude;
                _originalLongitude = Input.location.lastData.longitude;

                // Get the list of objects to show and their locations
                var url = "http://www.mission-base.com/ArvosVun.txt"
                    + "?version=1"
                    + "&lat=" + _originalLatitude.ToString("F6")
                    + "&lon=" + _originalLongitude.ToString("F6")
                    + "&version=1"
                    + "&channel=ArvosVun"
                    + "&device=" + SystemInfo.deviceUniqueIdentifier
                    ;

                UnityWebRequest www = UnityWebRequest.Get(url);
                yield return www.SendWebRequest();

                if (www.isNetworkError || www.isHttpError)
                {
                    _error = www.error;
                    yield break;
                }

                // Get results as text
                var text = www.downloadHandler.text;
                if (string.IsNullOrEmpty(text))
                {
                    _error = "WebRequest to url '" + url + "' received empty text.";
                    yield break;
                }

                // Create the objects
                CreateArObjects(text);
                if (!string.IsNullOrEmpty(_error))
                {
                    yield break;
                }

                if (_arObjects.Count == 0)
                {
                    _error = "Sorry, there are no augments at your location!";
                    yield break;
                }
                _startSecond = DateTime.Now.Ticks / 10000000;
                _initialHeading = Input.compass.trueHeading;
                _headingShown = Input.compass.trueHeading;
            }

            // For the first N seconds we remember the initial camera heading
            if (_cameraIsInitializing)
            {
                if (DateTime.Now.Ticks / 10000000 > _startSecond + 2)
                {
                    _cameraIsInitializing = false;
                }
            }

            // Overwrite current lat and lon everytime
            _currentLatitude = Input.location.lastData.latitude;
            _currentLongitude = Input.location.lastData.longitude;

            // Calculate positions for all ar objects
            PlaceArObjects();

            // Get the heading from the compass
            _currentHeading = Input.compass.trueHeading;

            yield return null;
        }
        yield return null;
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

    private GameObject FindGameObjectWithTag(string tag)
    {
        try
        {
            return GameObject.FindGameObjectWithTag(tag);
        }
        catch (Exception)
        {
            return null;
        }
    }

    void Start()
    {
        // Get references to objects
        _infoText = GameObject.FindGameObjectWithTag("distanceText");

        _sceneAnchor = FindGameObjectWithTag("SceneAnchor");
        if (_sceneAnchor == null)
        {
            _error = "Cannot find object with tag SceneAnchor";
            return;
        }

        _wrapper = FindGameObjectWithTag("Wrapper");
        if (_wrapper == null)
        {
            _error = "Cannot find object with tag Wrapper";
            return;
        }
        _cameraTransform = _wrapper.transform.parent;

        // Start GetCoordinate() function 
        StartCoroutine("GetCoordinates");
    }

    private long _currentSecond = DateTime.Now.Ticks / 10000000L;
    private int _fps = 30;
    private int _fpcs = 0;

    void Update()
    {
        var second = DateTime.Now.Ticks / 10000000L;
        if (_currentSecond == second)
        {
            _fpcs++;
        }
        else
        {
            if (_currentSecond == second - 1)
            {
                _fps = _fpcs + 1;
            }
            else
            {
                _fps = 1;
            }
            _fpcs = 0;
            _currentSecond = second;
        }

        // Set any error text on the canvas
        if (!string.IsNullOrEmpty(_error) && _infoText != null)
        {
            _infoText.GetComponent<Text>().text = _error;
            return;
        }

        // Calculate heading
        var currentHeading = _currentHeading;
        if (Math.Abs(currentHeading - _headingShown) > 180)
        {
            if (currentHeading < _headingShown)
            {
                currentHeading += 360;
            }
            else
            {
                _headingShown += 360;
            }
        }
        _headingShown += (currentHeading - _headingShown) / 10; //  (1 + _fps / 2);
        while (_headingShown > 360)
        {
            _headingShown -= 360;
        }

        // Place the ar objects
        _sceneAnchor.transform.eulerAngles = new Vector3(0, 0, 0);
        foreach (var arObject in _arObjects)
        {
            if (!arObject.IsRelative)
            {
                // Linearly interpolate from current position to target position
                var position = Vector3.Lerp(arObject.GameObject.transform.position, arObject.TargetPosition, .5f / _fps);
                arObject.GameObject.transform.position = position;
            }
        }
        _sceneAnchor.transform.eulerAngles = new Vector3(0, 360 - _initialHeading, 0);

        // Turn the ar objects
        if (_cameraIsInitializing)
        {
            _initialHeading = _headingShown;
            _initialCameraAngle = _cameraTransform.eulerAngles.y;
            foreach (var arObject in _arObjects)
            {
                arObject.GameObject.transform.eulerAngles = new Vector3(0, 360 - _initialHeading, 0);
            }
        }

        if (_infoText != null)
        {
            // Set info text
            if (!_showInfo)
            {
                _infoText.GetComponent<Text>().text = string.Empty;
                return;
            }

            _infoText.GetComponent<Text>().text =
                ""
                //+ "Z " + GetTarketPosition(_arObjects.LastOrDefault()).z.ToString("F1")
                //+ " X " + GetTarketPosition(_arObjects.LastOrDefault()).x.ToString("F1")
                //+ " Y " + (GetTarketPosition(_arObjects.LastOrDefault()).y.ToString("F1")
                //+ " LA " + (_currentLatitude).ToString("F6")
                //+ " LO " + (_currentLongitude).ToString("F6")
                //+ " F " + _fps.ToString("F") 
                + "IC " + _initialCameraAngle.ToString("F")
                + " C " + _cameraTransform.eulerAngles.y.ToString("F")
                + " SA " + _sceneAnchor.transform.eulerAngles.y.ToString("F")
                + " H " + _headingShown.ToString("F")
                ;
            // + " N " + _arObjects.Count
        }
    }

    private Vector3 GetTarketPosition(ArObject arObject)
    {
        if(arObject == null
)
        {
            return new Vector3(0, 0, 0);
        }
        return arObject.TargetPosition;
    }
}
