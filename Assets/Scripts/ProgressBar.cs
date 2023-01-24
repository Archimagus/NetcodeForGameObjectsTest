using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProgressBar : MonoBehaviour
{
	[SerializeField] RectTransform _fill;
	private float progress;

	public float Progress
	{
		get => progress; 
		set
		{
			progress = value;
			_fill.localScale = new Vector3(1,progress,1);
		}
	}
}
