using System;
using System.Collections.Generic;
using System.Linq;
using Data;
using Dreamteck.Splines;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DreamTeckExtensions
{
    [Serializable]
    public class SplineChannelLaneData//property drawer
    {
        public float _width;
        public float _xOffset;
        public List<ZoneLaneDesc> _channelLanes;//display lanes to make them editable, instantiate default zone lane desc
        //todo on value changed in property drawer:
        public int _lanesCount;//modify list when it changes, set lanes width within external channel width, for now simple equal distribution
    }
    
    public class DreamTeckSplineLaneProfileProvider : MonoBehaviour, IZoneLaneProfile
    {
        [SerializeField] private SplineComputer _splineComputer;//to get spline shape
        [SerializeField] private SplineMesh _splineMesh;//to get current channel count, offsets etc, for offsetting the spline debug.

        [SerializeField] private List<SplineChannelLaneData> _splineChannelLaneDatas;
        //todo need additional channel data, to bind with the channel and display and then serialize
        //todo it will be called by authoring component whenever the dependency is modified
        //todo need to expose each channel in separate debug system with offsets from the middle
        //and divide etc.
        //also the SplineComputer can have its own display that would match the visual channel settings,
        //it would display per channel, could have buttons like: Add channels as lanes, than each channel 
        //would receive editable spline, that could be doubled, widened etc.
        
        //when gathering data about the channels
        
        //todo display custom editor for each lane, private list of channels updated in OnValidate
        //todo then option like in zbrush split, number of splits, and distributes along the channel width evenly
        //TODO each channel has ChannelLanesData { ZoneLaneDesc[], channelWidth (mesh width, or this is to be set here), splineOffset,  } associated with it, but it has total width of its own, and number of lanes it has, and then each lane is configurable on its own (direction, width etc.)
        //todo bool in on between lanes as dividers, interlane width =  (next lane middle - half width) - (lane previous middle + half width)

        public void OnValidate()//todo instead register and listen to property channels
        {
            if (_splineComputer == null || _splineMesh == null)
            {
                return;
            }

            var channelCount = _splineMesh.GetChannelCount();
            _splineChannelLaneDatas ??= new List<SplineChannelLaneData>();

            for (int i = 0; i < channelCount; i++)
            {
                var channel = _splineMesh.GetChannel(i);
                var xOffset = channel.minOffset.x;
                if (_splineChannelLaneDatas.Count <= i)
                {
                    _splineChannelLaneDatas.Add(new SplineChannelLaneData());
                }

                var channelLaneData = _splineChannelLaneDatas[i];
                channelLaneData._xOffset = xOffset;
            }

            while (channelCount < _splineChannelLaneDatas.Count)
            {
                _splineChannelLaneDatas.RemoveAt(_splineChannelLaneDatas.Count - 1);
            }

            //if (_splineChannelLaneDatas.Count)
            _allLanes = _splineChannelLaneDatas.SelectMany(data => data?._channelLanes).ToArray();
        }

        private ZoneLaneDesc[] _allLanes = null;
        
        public ZoneLaneDesc[] GetLaneDescriptions()
        {
            return _allLanes;
        }

        public float GetLanesTotalWidth()
        {
            float totalWidth = 0.0f;
            foreach (var zoneLaneDesc in _allLanes)
            {
                totalWidth += zoneLaneDesc._width;
            }

            return totalWidth;
        }

        public Object GetSourceInstance()
        {
            return this;
        }
        
        public static void DrawArrow(Vector3 pos, Vector3 direction, float arrowLength, float arrowHeadLength = 0.25f, float arrowHeadAngle = 20.0f) {
            var arrowTip = pos + direction * arrowLength;
            Gizmos.DrawLine(pos, arrowTip);

            Camera c = Camera.current;
            if (c == null) return;
            Vector3 right = Quaternion.LookRotation(direction, c.transform.forward) * Quaternion.Euler(0, 180 + arrowHeadAngle, 0) * new Vector3(0, 0, 1);
            Vector3 left = Quaternion.LookRotation(direction, c.transform.forward) * Quaternion.Euler(0, 180 - arrowHeadAngle, 0) * new Vector3(0, 0, 1);
            Gizmos.DrawLine(arrowTip, arrowTip + right * arrowHeadLength);
            Gizmos.DrawLine(arrowTip, arrowTip + left * arrowHeadLength);
        }
    }
}