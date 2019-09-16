using System;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;

using RNG = UnityEngine.Random;

public class MazeButtons : MonoBehaviour
{
	// Standardized logging
	private static int globalLogID = 0;
	private int thisLogID;
	private bool moduleSolved;

	/*********************************************************************
		To Timwi, and/or anyone else writing Souvenir support for this
		or any of my other modules:  This is a request that you do not
		modify the functionality of my module or the appearance of any
		labels from within Souvenir itself.  I do not like the idea of
		another module changing the functionality of my own to fit its
		whims, and feel it results in some unwelcome surprises for the
		experts and defusers alike; especially newer ones or those who
		are still trying to learn the module.
	*********************************************************************/

	// To make things easier for Souvenir
#pragma warning disable 414
	private int    firstHoldNum   = -1;
	private string firstHoldColor = "";

	private int    lastHoldNum   = -1;
	private string lastHoldColor = "";
#pragma warning restore 414
	// -----

	public KMAudio bombAudio;
	public KMBombModule bombModule;
	public KMBombInfo bombInfo;

	public GameObject[] buttons;
	public Material[] colorMats;

	public KMSelectable resetButton;

	private KMSelectable[] bSelects;
	private TextMesh[] bLabels;
	private Renderer[] bRends;
	private Animator[] bAnims;

	private readonly string[] __maze = new string[] {
		"███ ███████████████████",
		"█     ░   █   █     ░ █",
		"█████░█ ███ █ █ ███ █░█",
		"█  X█ █X█  X█ █X  █X█ ░",
		"█ ███ █ █ ███░███ ███ █",
		"█ ░  X█ ░X█  X  ░X█   █",
		"█ █ █ █ ███ █████ ███ █",
		"█ █X█ █X  █X█  X█  X█ █",
		"█ ███░███░█ █ █ ███ █ █",
		"█    X   X█ █X█  X█   █",
		"█ ███ ███ █░█ ███░███░█",
		"█  X█ █X  ░X░ █X  █X█ █",
		"█░███ █ ███░█████ █ █ █",
		"█ ░ █X█  X   X░  X  █ █",
		"█ █ █ ███ ███ █ ███ █ █",
		"█ █X  █X  █X  █X  █X░ █",
		"█ █████░█████████░███ █",
		"█   █X  █X   X   X  ░ █",
		"███ █ █ █ ███████████ █",
		"░ █X  █X░ █X  █X   X█ █",
		"█ ███████ ███ █ █████ █",
		"█         ░     █     █",
		"███████████████████ ███",
	};

	//	Red	    | Abort
	//	Yellow  | Detonate
	//	Blue    | Press
	//	White   | Hold
	private readonly byte[,] __buttonData = new byte[,] {
		{0,    0x24, 0,    0x22, 0,    0x34, 0,    0x41, 0,    0x11, 0   },
		{0x23, 0,    0x14, 0,    0x44, 0,    0x31, 0,    0x23, 0,    0x22},
		{0,    0x32, 0,    0x34, 0,    0x12, 0,    0x42, 0,    0x33, 0   },
		{0x13, 0,    0x24, 0,    0x43, 0,    0x34, 0,    0x22, 0,    0x11},
		{0,    0x11, 0,    0x31, 0,    0x11, 0,    0x12, 0,    0x44, 0   },
		{0x31, 0,    0x23, 0,    0x12, 0,    0x13, 0,    0x23, 0,    0x13},
		{0,    0x21, 0,    0x42, 0,    0x14, 0,    0x32, 0,    0x31, 0   },
		{0x34, 0,    0x44, 0,    0x22, 0,    0x43, 0,    0x24, 0,    0x12},
		{0,    0x11, 0,    0x31, 0,    0x21, 0,    0x42, 0,    0x41, 0   },
		{0x23, 0,    0x32, 0,    0x44, 0,    0x31, 0,    0x14, 0,    0x43},
		{0,    0x24, 0,    0x22, 0,    0x33, 0,    0x33, 0,    0x21, 0   }
	};

	private readonly int[,,] __checkMoveOffsets = new int[,,] {
		// North     East      South     West
		{{ 0, -1}, { 1,  0}, { 0,  1}, {-1,  0}}, // No rotation
		{{-1,  0}, { 0, -1}, { 1,  0}, { 0,  1}}, // 90 degrees
		{{ 0,  1}, {-1,  0}, { 0, -1}, { 1,  0}}, // 180 degrees
		{{ 1,  0}, { 0,  1}, {-1,  0}, { 0, -1}}, // 270 degrees
	};

	private readonly string[] __directions = new string[] {
		"up", "right", "down", "left"
	};


	// -----
	// Handling the central seven-segment display
	// -----

	enum SevenSegmentColor {
		Glitchy = -1,
		Red = 1,
		Blue = 2,
		Green = 3,
		Yellow = 4,
		White = 5,
	};

	private byte[] SevenSegmentDisplaySetups = new byte[] {
		0x3F, 0x06, 0x5B, 0x4F, 0x66, 0x6D, 0x7D, 0x07, 0x7F, 0x6F,
	};

	public Renderer[] SevenSegments;
	public Material[] SevenSegMats;

	void SetUpSevenSegment(byte lit, SevenSegmentColor color)
	{
		int matNum;
		switch (color)
		{
			case SevenSegmentColor.Glitchy: matNum = RNG.Range(1,6); break;
			default: matNum = (int)color; break;
		}
		for(int i = 0; i < 7; ++i)
		{
			if ((lit & (1 << i)) > 0)
			{
				SevenSegments[i].material = SevenSegMats[matNum];
				SevenSegments[i].GetComponent<Animator>().Play("ColorPulsate", 0, 0);

				if (color == SevenSegmentColor.Glitchy)
					matNum = (((matNum - 1) + RNG.Range(1,5)) % 5) + 1;
			}
			else
				SevenSegments[i].material = SevenSegMats[0];
		}
	}

	void ClearSevenSegment()
	{
		for(int i = 0; i < 7; ++i)
		{
			SevenSegments[i].material = SevenSegMats[0];
			SevenSegments[i].GetComponent<Animator>().Play("ColorSolid", 0, 0);
		}
	}

	void ShowErrorOnSevenSegment(byte lit, SevenSegmentColor color)
	{
		for(int i = 0; i < 7; ++i)
		{
			if ((lit & (1 << i)) > 0)
			{
				SevenSegments[i].material = SevenSegMats[(int)color];
				SevenSegments[i].GetComponent<Animator>().Play("ColorRapidFlashing", 0, 0);
			}
			else
				SevenSegments[i].material = SevenSegMats[0];
		}
	}


	// -----
	// Module setup
	// -----

	private int cXStart, cYStart;
	private int cX, cY;

	private int rotation = 0; // 0 = none, 1 = 90, 2 = 180, 3 = 270

	void GenerateStart()
	{
		// So, what's our rotation?
		int rotationCheck = RNG.Range(0,100);

		if (rotationCheck <= 33)      rotation = 0; // No rotation
		else if (rotationCheck <= 55) rotation = 1; // 90 degrees CW
		else if (rotationCheck <= 77) rotation = 3; // 90 degrees CCW
		else                          rotation = 2; // 180 degrees

		// Generate random positions
		cY = RNG.Range(1, 10);
		if ((cY & 1) == 0) // On an even row
			cX = RNG.Range(1, 5) << 1;
		else // On an odd row
			cX = (RNG.Range(1, 6) << 1) - 1;

		string[] debugButtons = new string[4]; // To log what buttons are on the module.
		for (int i = 0; i < 4; ++i)
		{
			string color = "???";

			// stored [Y, X] because I'm a fool
			byte bData = __buttonData[cY + __checkMoveOffsets[rotation, i, 1], cX + __checkMoveOffsets[rotation, i, 0]];

			// The low nibble of the data determines the label.
			switch (bData & 0x0F)
			{
				case 1: bLabels[i].text = "Abort";    break;
				case 2:
					bLabels[i].text = "Detonate";
					// Due to size, shrink the text.
					bLabels[i].characterSize = 0.25f;
					break;
				case 3: bLabels[i].text = "Press";    break;
				case 4: bLabels[i].text = "Hold";     break;
				default: /* Shouldn't happen. */      break;
			}

			// The high nibble of the data determines the color.
			bRends[i].material = colorMats[((bData & 0xF0) >> 4) - 1];
			switch (bData & 0xF0)
			{
				case 16: color = "Red";    goto default;
				case 32: color = "Yellow"; break;
				case 48: color = "Blue";   goto default;
				case 64: color = "White";  break;
				default: // Lighten text for any cases that need it.
					bLabels[i].color = new Color(1f, 1f, 1f, 1f);
					break;
			}

			debugButtons[i] = String.Format("{0} {1}", color, bLabels[i].text);

		}
		Debug.LogFormat("[A-maze-ing Buttons #{0}] Clockwise starting from north, your buttons are \"{1}\", \"{2}\", \"{3}\", and \"{4}\".",
			thisLogID, debugButtons[0], debugButtons[1], debugButtons[2], debugButtons[3]);

		Debug.LogFormat("[A-maze-ing Buttons #{0}] Your starting location is ({1}, {2}) on the map.",
			thisLogID, (cX + 1), (cY + 1));

		if (rotation == 0)
			Debug.LogFormat("[A-maze-ing Buttons #{0}] There is no rotation.", thisLogID);
		else if (rotation == 1)
			Debug.LogFormat("[A-maze-ing Buttons #{0}] There is a 90 degree clockwise rotation.", thisLogID);
		else if (rotation == 3)
			Debug.LogFormat("[A-maze-ing Buttons #{0}] There is a 90 degree counter-clockwise rotation.", thisLogID);
		else
			Debug.LogFormat("[A-maze-ing Buttons #{0}] There is a 180 degree rotation.", thisLogID);

		// Put in correct maze position
		++cX;
		++cY;
		cX <<= 1;
		cY <<= 1;
		cXStart = --cX;
		cYStart = --cY;
	}


	// -----
	// Button handling
	// -----

	private int currentButton = -1; // What button is currently being pressed
	// (This is also checked by Twitch Plays to prevent working with more than one button at a time)

	private bool buttonHeld; // If currently being held (checked on release)
	private int holdDigit; // What digit is being displayed (-1 is non-digit)
	private byte holdSegments; // Segments that make up the current display
	private SevenSegmentColor holdColor; // Color of the display

	// So it can be stopped on button release.
	Coroutine holdTracker;

	IEnumerator ButtonHoldTracker()
	{
		buttonHeld = false;

		// Releasing before this point is considered a TAP.
		yield return new WaitForSeconds(0.5f);
		// Releasing after this point is considered a HOLD.

		int whichColor = RNG.Range(0, 100);
		if (whichColor < 18)      holdColor = SevenSegmentColor.Red;
		else if (whichColor < 36) holdColor = SevenSegmentColor.Blue;
		else if (whichColor < 54) holdColor = SevenSegmentColor.Green;
		else if (whichColor < 72) holdColor = SevenSegmentColor.Yellow;
		else if (whichColor < 90) holdColor = SevenSegmentColor.White;
		else                      holdColor = SevenSegmentColor.Glitchy;

		// Previously it was possible to obtain seven segment display readings that didn't form a number.
		// However, this was removed to reduce rule complexity.
/*		if (holdDigit == -1)
		{
			do
			{
				int i = 0;
				byte nextAdd;

				holdSegments = 0;
				while (i < 3)
				{
					nextAdd = (byte)(1 << RNG.Range(0,7));
					if ((holdSegments & nextAdd) != 0)
						continue;
					holdSegments |= nextAdd;
					++i;
				}
			} while (holdSegments == 0x07); // Don't allow randomly generating sevens

			Debug.LogFormat("[A-maze-ing Buttons #{0}] Seven segment display is now showing: a {1} non-number (lit segments: {2})",
				thisLogID, holdColor.ToString(), Convert.ToString(holdSegments, 2).PadLeft(7, '0'));
		} */

		holdDigit = RNG.Range(0, 10);
		holdSegments = SevenSegmentDisplaySetups[holdDigit];

		Debug.LogFormat("[A-maze-ing Buttons #{0}] Seven segment display is now showing: a {1} {2}",
			thisLogID, holdColor.ToString(), holdDigit);

		lastHoldNum = holdDigit;
		lastHoldColor = holdColor.ToString();
		if (firstHoldNum == -1)
		{
			firstHoldNum = holdDigit;
			firstHoldColor = holdColor.ToString();
		}

		SetUpSevenSegment(holdSegments, holdColor);
		buttonHeld = true;
		yield break;
	}

	bool IsReleaseTimeCorrect()
	{
		int tSeconds = (int)bombInfo.GetTime() % 60;
		bool valid = true;
		string invalidStr = "";

		// If the display shows more than one color, and the displayed digit is 0 or 1, release when the number of seconds remaining is a triangular number.
		if (holdColor == SevenSegmentColor.Glitchy && (holdDigit == 0 || holdDigit == 1))
		{
			valid = Array.IndexOf(new int[] { 0, 1, 3, 6, 10, 15, 21, 28, 36, 45, 55 }, tSeconds) != -1;
			invalidStr = "the number of seconds is a triangular number; 0, 1, 3, 6, 10, 15, 21, 28, 36, 45, or 55";
		}

		// If the displayed digit is 0, release when the number of seconds remaining ends in 5.
		else if (holdDigit == 0)
		{
			valid = tSeconds % 10 == 5;
			invalidStr = "the number of seconds ends in 5";
		}

		// If the display shows more than one color, and the displayed digit is prime, release when the number of seconds remaining is prime.
		else if (holdColor == SevenSegmentColor.Glitchy && (holdDigit == 2 || holdDigit == 3 || holdDigit == 5 || holdDigit == 7))
		{
			valid = Array.IndexOf(new int[] { 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37, 41, 43, 47, 53, 59}, tSeconds) != -1;
			invalidStr = (tSeconds == 1)
				? "the number of seconds is a prime number; 1 is not prime by definition"
				: "the number of seconds is a prime number";
		}

		// If the display is completely blue, and the displayed digit is less than 3 or greater than 7, release when both seconds digits are even.
		else if (holdColor == SevenSegmentColor.Blue && (holdDigit < 3 || holdDigit > 7))
		{
			valid = ((tSeconds & 1) == 0 && ((tSeconds / 10) & 1) == 0);
			invalidStr = "both seconds digits are even";
		}

		// If the display is completely red, release when the displayed digit is present on either seconds digit.
		else if (holdColor == SevenSegmentColor.Red)
		{
			valid = (tSeconds % 10 == holdDigit || tSeconds / 10 == holdDigit);
			invalidStr = String.Format("either seconds digit is {0}", holdDigit);
		}

		// If the display is completely green, release when the displayed digit is _not_ present on either seconds digit.
		else if (holdColor == SevenSegmentColor.Green)
		{
			valid = !(tSeconds % 10 == holdDigit || tSeconds / 10 == holdDigit);
			invalidStr = String.Format("neither seconds digit is {0}", holdDigit);
		}

		// If the displayed digit is even, and the display is not completely blue, release when both seconds digits are odd.
		else if ((holdDigit & 1) == 0 && holdColor != SevenSegmentColor.Blue)
		{
			valid = ((tSeconds & 1) == 1 && ((tSeconds / 10) & 1) == 1);
			invalidStr = "both seconds digits are odd";
		}

		// If the display is completely yellow, release when the number of seconds remaining is a multiple of the displayed digit.
		else if (holdColor == SevenSegmentColor.Yellow)
		{
			valid = (tSeconds % holdDigit) == 0;
			invalidStr = String.Format("the number of seconds is a multiple of {0}", holdDigit);
		}

		// Otherwise, release when the displayed digit is present on exactly one seconds digit.
		else
		{
			valid = (tSeconds % 10 == holdDigit ^ tSeconds / 10 == holdDigit);
			invalidStr = String.Format("exactly one seconds digit is {0}", holdDigit);
		}

		if (!valid)
		{
			Debug.LogFormat("[A-maze-ing Buttons #{0}] STRIKE: I was expecting a time when {2}. You released at xx:{1}, which was incorrect.", 
				thisLogID, tSeconds.ToString().PadLeft(2, '0'), invalidStr);
			bombModule.HandleStrike();
		}
		else
			Debug.LogFormat("[A-maze-ing Buttons #{0}] I was expecting a time when {2}. You released at xx:{1}, which was correct.", 
				thisLogID, tSeconds.ToString().PadLeft(2, '0'), invalidStr);
		return valid;
	}

	bool ButtonPress(int button)
	{
		if (currentButton != -1)
			return false;

		currentButton = button;
		bombAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.BigButtonPress, bSelects[button].transform);
		bAnims[button].Play("ButtonPress", 0, 0);
		bSelects[button].AddInteractionPunch(0.4f);

		if (moduleSolved)
			return false;

		ClearSevenSegment();
		holdTracker = StartCoroutine(ButtonHoldTracker());

		return false;
	}

	void ButtonRelease(int button)
	{
		if (button != currentButton)
			return;

		currentButton = -1;
		bombAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.BigButtonRelease, bSelects[button].transform);
		bAnims[button].Play("ButtonRelease", 0, 0);

		if (moduleSolved)
			return;

		StopCoroutine(holdTracker);
		ClearSevenSegment();

		int mX = cX + __checkMoveOffsets[rotation, button, 0], mY = cY + __checkMoveOffsets[rotation, button, 1];
		int nX = cX + (__checkMoveOffsets[rotation, button, 0] * 2), nY = cY + (__checkMoveOffsets[rotation, button, 1] * 2);

		if (!buttonHeld)
		{
			if (__maze[mY][mX] == '░')
			{
				Debug.LogFormat("[A-maze-ing Buttons #{0}] STRIKE: Tried to tap to move from ({1}, {2}) to ({3}, {4}). You need to hold to do that.", 
					thisLogID, (cX + 1) / 2, (cY + 1) / 2, (nX + 1) / 2, (nY + 1) / 2);
				bombModule.HandleStrike();
				ShowErrorOnSevenSegment(0x40, SevenSegmentColor.Yellow);
				return;
			}
		}
		else
		{
			// Handles the strike itself if it's necessary
			if (!IsReleaseTimeCorrect())
				return;
			if (__maze[mY][mX] == ' ')
			{
				Debug.LogFormat("[A-maze-ing Buttons #{0}] STRIKE: Tried to hold to move from ({1}, {2}) to ({3}, {4}). You need to tap to do that.", 
					thisLogID, (cX + 1) / 2, (cY + 1) / 2, (nX + 1) / 2, (nY + 1) / 2);
				bombModule.HandleStrike();
				ShowErrorOnSevenSegment(0x40, SevenSegmentColor.Green);
				return;
			}
		}

		if (__maze[mY][mX] == '█')
		{
			Debug.LogFormat("[A-maze-ing Buttons #{0}] STRIKE: Tried to move from ({1}, {2}) to ({3}, {4}). There's a solid wall blocking your path.", 
				thisLogID, (cX + 1) / 2, (cY + 1) / 2, (nX + 1) / 2, (nY + 1) / 2);
			bombModule.HandleStrike();
			ShowErrorOnSevenSegment(0x40, SevenSegmentColor.Red);
			return;
		}

		cX = nX;
		cY = nY;

		if (cX < 0 || cX > 22 || cY < 0 || cY > 22)
		{
			bombAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, bombModule.transform);
			Debug.LogFormat("[A-maze-ing Buttons #{0}] SOLVE: Left the maze successfully at ({1}, {2}).",
				thisLogID, (cX + 1) / 2, (cY + 1) / 2);
			bombModule.HandlePass();
			moduleSolved = true;
		}
	}


	// -----
	// Reset handling
	// -----

	IEnumerator ResetHoldTracker()
	{
		yield return new WaitForSeconds(0.125f);
		SevenSegments[0].material = SevenSegMats[(int)SevenSegmentColor.Red];
		yield return new WaitForSeconds(0.125f);
		SevenSegments[1].material = SevenSegMats[(int)SevenSegmentColor.Red];
		yield return new WaitForSeconds(0.125f);
		SevenSegments[2].material = SevenSegMats[(int)SevenSegmentColor.Red];
		yield return new WaitForSeconds(0.125f);
		SevenSegments[3].material = SevenSegMats[(int)SevenSegmentColor.Red];
		yield return new WaitForSeconds(0.125f);
		SevenSegments[4].material = SevenSegMats[(int)SevenSegmentColor.Red];
		yield return new WaitForSeconds(0.125f);
		SevenSegments[5].material = SevenSegMats[(int)SevenSegmentColor.Red];
		yield return new WaitForSeconds(0.125f);
		SevenSegments[6].material = SevenSegMats[(int)SevenSegmentColor.Red];

		for (int i = 0; i < 7; ++i)
			SevenSegments[i].GetComponent<Animator>().Play("ColorRapidFlashing", 0, 0);

		// Explicitly wait for the flashing to *start*.
		yield return new WaitForSeconds(0.125f);

		cX = cXStart;
		cY = cYStart;
		firstHoldNum   = -1;
		firstHoldColor = "";
		lastHoldNum    = -1;
		lastHoldColor  = "";
		Debug.LogFormat("[A-maze-ing Buttons #{0}] Returned to the starting position.", thisLogID);
		yield break;
	}

	bool ResetButtonPress()
	{
		if (moduleSolved || currentButton != -1)
			return false;

		ClearSevenSegment();
		holdTracker = StartCoroutine(ResetHoldTracker());

		bombAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, resetButton.transform);
		currentButton = -2;

		return false;
	}

	void ResetButtonRelease()
	{
		if (currentButton != -2)
			return;

		StopCoroutine(holdTracker);
		ClearSevenSegment();

		bombAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonRelease, resetButton.transform);
		currentButton = -1;
	}


	// -----
	// Module dirty work
	// -----

	void Awake()
	{
		thisLogID = ++globalLogID;

		bSelects = new KMSelectable[buttons.Length];
		bLabels = new TextMesh[buttons.Length];
		bRends = new Renderer[buttons.Length];
		bAnims = new Animator[buttons.Length];
		for (int i = 0; i < buttons.Length; ++i)
		{
			int j = i;

			bSelects[i] = buttons[i].GetComponentInChildren<KMSelectable>();
			bLabels[i] = buttons[i].GetComponentInChildren<TextMesh>();
			bRends[i] = bSelects[i].GetComponent<Renderer>();
			bAnims[i] = buttons[i].GetComponent<Animator>();

			bSelects[i].OnInteract += delegate() { return ButtonPress(j); };
			bSelects[i].OnInteractEnded += delegate() { ButtonRelease(j); };
		}

		resetButton.OnInteract += ResetButtonPress;
		resetButton.OnInteractEnded += ResetButtonRelease;

		GenerateStart();
	}


	// -----
	// Twitch Plays Support
	// -----

	private KMSelectable CharToButton(char c)
	{
		switch (c)
		{
			case 'N': case 'U': return bSelects[0];
			case 'E': case 'R': return bSelects[1];
			case 'S': case 'D': return bSelects[2];
			default:            return bSelects[3];
		}
		// Unreachable
	}

	private bool InputAllowedCheck(out string errorMsg)
	{
		errorMsg = "";

		if (currentButton >= 0)
		{
			errorMsg = String.Format("sendtochaterror Hey, are you going to release the {0} button or what? I can't do anything until you do that.", __directions[currentButton]);
			return false;
		}
		else if (currentButton < -1)
		{
			// There should never be a case where reset is held in TP in the middle of a command, but just in case.
			errorMsg = "sendtochaterror Take your finger off the display first!";
			return false;
		}
		return true;
	}

	enum ReleaseType {
		Invalid = -1,
		Any = 1,
		Present = 2,
		NotPresent = 3,
		Exact = 4
	};

	private ReleaseType GetReleaseType(string command, out int[] args)
	{
		Match mt;

		args = null;

		if (Regex.IsMatch(command, @"^\s*release\s*any\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
			return ReleaseType.Any;
		else if ((mt = Regex.Match(command, @"^\s*release\s*(?:on\s*)?(not\s*|)([\d])\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).Success)
		{
			args = new int[] { mt.Groups[2].ToString()[0] - '0' };
			return mt.Groups[1].ToString().StartsWith("not") ? ReleaseType.NotPresent : ReleaseType.Present;
		}
		else if (Regex.IsMatch(command, @"^\s*release\s*(?:on\s*)?([\d]{2})\s*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
		{
			string[] cList = command.Split(' ');
			List<int> times = new List<int>();
			try
			{
				int t;
				for (int i = 1; i < cList.Length; ++i)
				{
					if (cList[i].Equals("on"))
						continue;

					t = int.Parse(cList[i]);
					if (t < 0 || t > 59)
						return ReleaseType.Invalid;

					times.Add(t);
				}
			}
			catch (Exception) 
			{
				return ReleaseType.Invalid;
			}

			if (times.Count == 0)
				return ReleaseType.Invalid;

			args = times.ToArray();
			return ReleaseType.Exact;
		}
		return ReleaseType.Invalid;
	}

	private int GetNextReleaseTime(ReleaseType type, int[] args, float currentTime, int timerDirection)
	{
		// To compensate for slight delays in TP releasing the button:
		// Outside Zen mode
		// If the timer is at xx.50 or greater, we'll try the timer value it's currently on. (30.50 = 30)
		// If the timer is at xx.49 or lower, we'll skip the time it's currently on and move to the next. (30.49 = 29)
		// In Zen mode
		// If the timer is at xx.49 or lower, we'll try the timer value it's currently on. (30.49 = 30)
		// If the timer is at xx.50 or greater,  we'll skip the time it's currently on and move to the next. (30.50 = 31)
		int targetTime = (int)(currentTime + (timerDirection * 0.5f));
		switch (type)
		{
			case ReleaseType.Present:
				// args[0] == Requested digit to be present
				while (!(targetTime % 10 == args[0] ^ (targetTime % 60) / 10 == args[0]))
				{
					if ((targetTime += timerDirection) < 0)
						break;
				}
				break;
			case ReleaseType.NotPresent:
				// args[0] == Requested digit to be absent
				while (targetTime % 10 == args[0] || (targetTime % 60) / 10 == args[0])
				{
					if ((targetTime += timerDirection) < 0)
						break;
				}
				break;
			case ReleaseType.Exact:
				// args == List of seconds digits to try to use
				int i = 0;
				for (; i < 120; ++i)
				{
					if (Array.IndexOf(args, targetTime % 60) != -1)
						break;
					if ((targetTime += timerDirection) < 0)
						break;
				}
				if (i == 120) // Realistically this should never happen. This is here to catch if it somehow does and return an error.
					targetTime = -1;
				break;
			default: // Should be unreachable
				break;
		}

		return targetTime;
	}

#pragma warning disable 414
	private readonly string TwitchHelpMessage = @"'!{0} tap UUDDLRLR' | '!{0} hold U' | '!{0} release any' (any time) | '!{0} release on 4' (4 on either digit) | '!{0} release on not 2' (2 not present) | '!{0} release on 06 12 18' (exact number of seconds) | '!{0} reset' (return to the start)";
#pragma warning restore 414

	private bool TwitchZenMode = false;

	public IEnumerator ProcessTwitchCommand(string command)
	{
		Match mt;
		string errorString;

		// Replace words for cardinal directions with single letters.
		command = Regex.Replace(command, @"(north|east|south|west|up|down|left|right)(\s+|$)", delegate(Match rpmt) {
			return rpmt.Value[0].ToString();
		}, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

		if ((mt = Regex.Match(command, @"^\s*(?:press|tap)\s+([newsUDLR]+)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).Success)
		{
			// I hate this.
			List<char> buttonChain = mt.Groups[1].ToString().ToUpper().ToCharArray().ToList();

			if (buttonChain.Count > 32)
				yield break; // Excessively long possibly troll command

			if (!InputAllowedCheck(out errorString))
			{
				yield return errorString;
				yield break;
			}

			yield return null;
			while (buttonChain.Count > 0)
			{
				char b = buttonChain[0];
				buttonChain.RemoveAt(0);

				KMSelectable whichButton = CharToButton(b);
				yield return whichButton;
				yield return new WaitForSeconds(0.1f);
				yield return whichButton;
				yield return new WaitForSeconds(0.15f);
			}
		}
		else if ((mt = Regex.Match(command, @"^\s*hold\s+([newsUDLR])\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).Success)
		{
			if (!InputAllowedCheck(out errorString))
			{
				yield return errorString;
				yield break;
			}

			char b = mt.Groups[1].ToString().ToUpper()[0];

			yield return null;
			yield return CharToButton(b);
		}
		else if (Regex.IsMatch(command, @"^\s*reset\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
		{
			if (!InputAllowedCheck(out errorString))
			{
				yield return errorString;
				yield break;
			}

			yield return null;
			yield return resetButton;
			yield return new WaitForSeconds(1.75f);
			yield return resetButton;
		}
		else if (Regex.IsMatch(command, @"^\s*release\s", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
		{
			int[] rArgs;
			ReleaseType rType = GetReleaseType(command, out rArgs);
			KMSelectable buttonToRelease;

			if (rType == ReleaseType.Invalid)
				yield break;

			if (currentButton == -1)
			{
				yield return "sendtochaterror Trying to release a button works much better when you've actually held one down, you know.";
				yield break;
			}
			buttonToRelease = (currentButton < 0) ? resetButton : bSelects[currentButton];

			// Command valid at this point. Get the bomb in position to release.
			yield return null;

			// Release whenever. In other words, right now and get out of here.
			if (rType == ReleaseType.Any)
			{
				yield return buttonToRelease;
				yield break;
			}

			int targetTime = GetNextReleaseTime(rType, rArgs, bombInfo.GetTime(), (TwitchZenMode) ? 1 : -1);

			if (targetTime < 0)
			{
				yield return "sendtochaterror I hate to be the bearer of bad news, but that time has passed and waiting another minute isn't an option.";
				yield break;
			}

			yield return String.Format("sendtochat Releasing the {0} button at {1:D2}:{2:D2}.", __directions[currentButton], targetTime/60, targetTime%60);

			if (TwitchZenMode)
			{
				if (targetTime - (int)bombInfo.GetTime() > 15)
					yield return "waiting music";
				while ((int)bombInfo.GetTime() < targetTime)
					yield return "trycancel your request to release the button was cancelled.";
			}
			else 
			{
				if ((int)bombInfo.GetTime() - targetTime > 15)
					yield return "waiting music";
				while ((int)bombInfo.GetTime() > targetTime)
					yield return "trycancel your request to release the button was cancelled.";
			}

			if ((int)bombInfo.GetTime() == targetTime)
				yield return bSelects[currentButton];
			else
				yield return "sendtochaterror The button was not released because the scheduled time was missed.";
		}
		yield break;
	}

	void TwitchHandleForcedSolve()
	{
		if (moduleSolved)
			return;

		Debug.LogFormat("[A-maze-ing Buttons #{0}] SOLVE: Twitch Plays requested a solve.", thisLogID);
		ClearSevenSegment();
		bombModule.HandlePass();
		moduleSolved = true;
	}
}
