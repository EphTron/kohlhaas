using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CharacterSwitcher : MonoBehaviour
{
    public List<CharacterIKController> characters = new(); // List of characters to switch between
    public List<GameObject> lights = new(); // List of lights for each character to switch between
    public MovementMapper movementMapper; // Reference to the MovementMapper for input handling
    public InputActionProperty switchCharacterAction; // Input action to switch characters
    private int currentCharacterIndex = 0; // Index of the currently active character

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (characters == null || characters.Count == 0)
        {
            Debug.LogError("No characters assigned to CharacterSwitcher.");
            return;
        }
        SetActiveCharacter(0);
    }

    // Update is called once per frame
    void Update()
    {
        if (switchCharacterAction.action.WasReleasedThisFrame())
        {
            currentCharacterIndex++;
            if (currentCharacterIndex >= characters.Count)
            {
                currentCharacterIndex = 0; // Loop back to the first character
            }
            SetActiveCharacter(currentCharacterIndex); // Switch to the next character
            movementMapper.Recenter(); // Recenter the movement mapper to the new character
        }
    }

    // Method to set the active character and update lights
    public void SetActiveCharacter(int index)
    {
        if (index < 0 || index >= characters.Count)
        {
            Debug.LogError("Invalid character index.");
            return;
        }
        currentCharacterIndex = index;
        movementMapper.currentCharacter = characters[currentCharacterIndex];
        movementMapper.Recenter();

        // Update lights based on the active character
        for (int i = 0; i < lights.Count; i++)
        {
            lights[i].SetActive(i == currentCharacterIndex);
        }
    }
}
