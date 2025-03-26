using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

public class BurgerGameManager : MonoBehaviour
{
    public enum GameState
    {
        WaitingForPlate,
        BuildingBurger,
        Completed
    }

    [SerializeField]
    private ObjectSpawner objectSpawner;

    [SerializeField]
    private TMP_Text recipeText; 

    [SerializeField] 
    private float stackingHeight = 0.05f; // Default height

    [SerializeField]
    private string plateObjectName = "Plate";

    private GameState currentState = GameState.WaitingForPlate;
    private List<string> recipe = new List<string>();
    private List<string> placedIngredients = new List<string>();
    private GameObject plateObject;
    private Vector3 lastStackPosition;
    private float currentStackHeight;

    private Dictionary<string, string> ingredientDisplayNames = new Dictionary<string, string>
    {
        { "Plate", "Plate" },
        { "BottomBread", "Bottom Bread" },
        { "TopBread", "Top Bread" },
        { "Lettuce", "Lettuce" },
        { "Onion", "Onion" },
        { "Cheese", "Cheese" },
        { "Tomato", "Tomato" },
        { "Burger", "Burger" }
    };

    private Dictionary<string, float> ingredientHeights = new Dictionary<string, float>
    {
        { "Plate", 0.02f },
        { "BottomBread", 0.1f },
        { "TopBread", 0.1f },
        { "Lettuce", 0.02f },
        { "Onion", 0.02f },
        { "Cheese", 0.01f },
        { "Tomato", 0.05f },
        { "Burger", 0.05f }
    };

    void Start()
    {
        if (objectSpawner != null)
        {
            objectSpawner.objectSpawned += OnObjectSpawned;
        }
        else
        {
            Debug.LogError("ObjectSpawner reference is missing!", this);
        }

        UpdateRecipeUI("Place the plate to start the game!");
    }

    void OnDestroy()
    {
        if (objectSpawner != null)
        {
            objectSpawner.objectSpawned -= OnObjectSpawned;
        }
    }

    private void OnObjectSpawned(GameObject spawnedObject)
    {
        string ingredientName = GetIngredientNameFromObject(spawnedObject);

        if (currentState == GameState.WaitingForPlate)
        {
            if (ingredientName == plateObjectName)
            {
                // Plate was placed - start the game
                plateObject = spawnedObject;
                lastStackPosition = plateObject.transform.position;
                currentStackHeight = 0f;
                currentState = GameState.BuildingBurger;
                
                GenerateRandomRecipe();
                UpdateRecipeUI(GetRecipeDisplayText());
                Debug.Log("Plate placed! Now build the burger according to the recipe.");
            }
            else
            {
                // Not a plate, destroy it
                Debug.Log("Please place a plate first!");
                Destroy(spawnedObject);
            }
        }
        else if (currentState == GameState.BuildingBurger)
        {
            // Position the ingredient above the last one
            PositionIngredient(spawnedObject);
            
            // Track the placed ingredient
            placedIngredients.Add(ingredientName);
            
            Debug.Log($"Added {ingredientName} to the burger. ({placedIngredients.Count}/{recipe.Count})");
            
            // Check if we've placed all ingredients
            if (placedIngredients.Count == recipe.Count)
            {
                CheckBurger();
            }
        }
        else if (currentState == GameState.Completed)
        {
            // Game is completed, but we can allow placing more objects
            PositionIngredient(spawnedObject);
        }
    }

    private void PositionIngredient(GameObject ingredient)
    {
        string ingredientName = GetIngredientNameFromObject(ingredient);
        
        // Place the ingredient at the current stack position
        Vector3 newPosition = new Vector3(
            lastStackPosition.x, 
            lastStackPosition.y + currentStackHeight,
            lastStackPosition.z
        );
        
        ingredient.transform.position = newPosition;
        lastStackPosition = newPosition;
        
        // Update the stack height for the next ingredient based on this ingredient's height
        if (ingredientHeights.TryGetValue(ingredientName, out float height))
        {
            currentStackHeight = height;
        }
        else
        {
            currentStackHeight = stackingHeight;
        }
    }

    private string GetIngredientNameFromObject(GameObject ingredient)
    {
        // Strip "(Clone)" and find the base name of the prefab
        string fullName = ingredient.name.Replace("(Clone)", "").Trim();
        
        // Go through our known ingredients and check if the name contains them
        foreach (string knownIngredient in ingredientDisplayNames.Keys)
        {
            if (fullName.Contains(knownIngredient))
            {
                return knownIngredient;
            }
        }
        
        return fullName;
    }

    private void GenerateRandomRecipe()
    {
        recipe.Clear();
        placedIngredients.Clear();

        // First ingredient after plate is always bottom bread
        recipe.Add("BottomBread");
        
        // Add 2-4 random ingredients
        string[] possibleIngredients = { "Lettuce", "Onion", "Cheese", "Tomato", "Burger" };
        int ingredientCount = Random.Range(2, 5);
        
        for (int i = 0; i < ingredientCount; i++)
        {
            string randomIngredient = possibleIngredients[Random.Range(0, possibleIngredients.Length)];
            recipe.Add(randomIngredient);
        }
        
        // Last ingredient is always top bread
        recipe.Add("TopBread");
    }

    private string GetRecipeDisplayText()
    {
        string text = "Make a burger with:\n";
        
        foreach (string ingredient in recipe)
        {
            if (ingredientDisplayNames.TryGetValue(ingredient, out string displayName))
            {
                text += "- " + displayName + "\n";
            }
            else
            {
                text += "- " + ingredient + "\n";
            }
        }
        
        text += "\n(Place ingredients in order from bottom to top)";
        return text;
    }

    private void UpdateRecipeUI(string text)
    {
        if (recipeText != null)
        {
            recipeText.text = text;
        }
    }

    private void CheckBurger()
    {
        bool isCorrect = true;
        
        // Check if recipe matches placed ingredients
        for (int i = 0; i < recipe.Count; i++)
        {
            if (i >= placedIngredients.Count || recipe[i] != placedIngredients[i])
            {
                isCorrect = false;
                break;
            }
        }
        
        if (isCorrect)
        {
            currentState = GameState.Completed;
            Debug.Log("Correct! You built the burger perfectly!");
            UpdateRecipeUI("Correct! You built the perfect burger!\n\nPlace plate again to start a new game.");

        }
        else
        {
            Debug.Log("Incorrect. Your burger doesn't match the recipe.");
            UpdateRecipeUI("Incorrect burger! Try again.\n" + GetRecipeDisplayText());
            
            // Allow the player to try again
            placedIngredients.Clear();
            
            // Reset stack height but keep the plate position
            currentStackHeight = 0f;
            lastStackPosition = plateObject.transform.position;
        }
    }

    // Call this method to reset the game (can be called from a UI button)
    public void ResetGame()
    {
        currentState = GameState.WaitingForPlate;
        placedIngredients.Clear();
        recipe.Clear();
        
        UpdateRecipeUI("Place the plate to start the game!");
        
        // Destroy all spawned objects
        if (objectSpawner != null && objectSpawner.transform.childCount > 0)
        {
            foreach (Transform child in objectSpawner.transform)
            {
                Destroy(child.gameObject);
            }
        }
    }
}
