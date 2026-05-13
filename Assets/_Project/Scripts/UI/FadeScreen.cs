using System.Collections;
using UnityEngine;

public class FadeScreen : MonoBehaviour
{
    [SerializeField] private GameObject fadePlane;
    public bool fadeOnStart = true;
	public float fadeDuration = 2f;
	public Color fadeColor = Color.black;

	private Renderer rend;
	private Coroutine fadeRoutine;

	private void Awake()
	{
		rend = GetComponent<Renderer>();

        if (fadeOnStart)
        {
            FadeIn();
        }

	}

	public void FadeIn()
	{
		if (fadePlane != null)
			fadePlane.SetActive(true);
		Fade(1f, 0f, true);
	}

	public void FadeOut()
	{
		if (fadePlane != null)
			fadePlane.SetActive(true);
		Fade(0f, 1f, false);
	}

	public void Fade(float alphaIn, float alphaOut, bool disablePlaneOnComplete)
	{
		if (fadeRoutine != null)
			StopCoroutine(fadeRoutine);

		fadeRoutine = StartCoroutine(FadeRoutine(alphaIn, alphaOut, disablePlaneOnComplete));
	}

	private IEnumerator FadeRoutine(float alphaIn, float alphaOut, bool disablePlaneOnComplete)
	{
		if (rend == null)
			yield break;

		float timer = 0f;

		while (timer <= fadeDuration)
		{
			Color newColor = fadeColor;
			newColor.a = Mathf.Lerp(alphaIn, alphaOut, timer / fadeDuration);
			rend.material.SetColor("_BaseColor", newColor);

			timer += Time.deltaTime;
			yield return null;
		}

		Color finalColor = fadeColor;
		finalColor.a = alphaOut;
		rend.material.SetColor("_BaseColor", finalColor);

		if (disablePlaneOnComplete && fadePlane != null)
			fadePlane.SetActive(false);

		fadeRoutine = null;
	}
}
