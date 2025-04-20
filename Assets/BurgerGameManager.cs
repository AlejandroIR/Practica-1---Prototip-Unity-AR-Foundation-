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
    private TMP_Text timerTXT;

    [SerializeField] 
    private TMP_Text scoreText;

    [SerializeField]
    private float stackingHeight = 0.05f; // Default height
    
    [SerializeField]
    private string plateObjectName = "Plate";
    
    [SerializeField]
    private float gameDuration = 90f; // 1 minute 30 seconds

    [Header("Some UI :)")]
    [SerializeField] private GameObject imagePlacePlate;
    [SerializeField] private GameObject imageRecipe;
    [SerializeField] private GameObject imageCorrectBurger;
    [SerializeField] private GameObject imageWrongBurger;

    [Header("SFX Clips")]
    [SerializeField] private AudioClip placementClip;       // colocar objeto
    [SerializeField] private AudioClip successClip;         // mision hecha
    [SerializeField] private AudioClip failClip;            // mision fallida
    [SerializeField] private AudioClip missionCompleteClip; //

    [Header("Music Clips")]
    [SerializeField] private AudioClip gameMusicClip;       // musica juego
    [SerializeField] private AudioClip menuMusicClip;       // musica menu

    [Header("Audio Sources")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource musicSource;


    private float currentTime;
    private int currentScore = 0;
    private bool timerRunning = false;

    private GameState currentState = GameState.WaitingForPlate;
    private List<string> recipe = new List<string>();
    private List<string> placedIngredients = new List<string>();
    private List<GameObject> placedIngredientObjects = new List<GameObject>();
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

        if (musicSource != null && gameMusicClip != null)
        {
            musicSource.clip = gameMusicClip;
            musicSource.loop = true;
            musicSource.Play();
        }

        UpdateRecipeUI("Place the plate to start the game!");
        imagePlacePlate.SetActive(true);
        imageRecipe.SetActive(false);
        UpdateScoreUI();
    }

    void Update()
    {
        if (timerRunning)
        {
            currentTime -= Time.deltaTime;
            
            if (currentTime <= 0)
            {
                currentTime = 0;
                timerRunning = false;
                GameOver();
            }
            
            UpdateTimerUI();
        }
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

        if (ingredientName == plateObjectName && plateObject != null && currentState != GameState.WaitingForPlate)
        {
            Debug.Log("Only one plate can be used at a time!");
            Destroy(spawnedObject);
            return;
        }

        if (currentState == GameState.WaitingForPlate)
        {
            if (ingredientName == plateObjectName)
            {
                // Plate was placed - start the game
                plateObject = spawnedObject;
                lastStackPosition = plateObject.transform.position;
                currentStackHeight = 0f;
                currentState = GameState.BuildingBurger;
                
                // Reset score at the beginning of a new game
                if (!timerRunning)
                {
                    currentScore = 0;
                    UpdateScoreUI();
                }
                
                // Start the timer when the first plate is placed
                StartTimer();
                
                GenerateRandomRecipe();
                imagePlacePlate.SetActive(false);
                imageRecipe.SetActive(true);
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

            if (sfxSource != null && placementClip != null) {sfxSource.PlayOneShot(placementClip);}

            // Position the ingredient above the last one
            PositionIngredient(spawnedObject);
            
            // Track the placed ingredient
            placedIngredients.Add(ingredientName);

            int index = placedIngredients.Count - 1;

            if (index < recipe.Count && placedIngredients[index] != recipe[index])
            {
                CheckBurger();
            }

            // Store reference to the placed ingredient object
            if (ingredientName != plateObjectName) {
                placedIngredientObjects.Add(spawnedObject);
            }
            
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

        if (!recipe.Contains("Burger"))
        {
            recipe.Add("Burger");
        }

        // Last ingredient is always top bread
        recipe.Add("TopBread");
    }

    private string GetRecipeDisplayText()
    {
        imageCorrectBurger.SetActive(false);
        imageWrongBurger.SetActive(false);
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

    private void UpdateTimerUI()
    {
        int minutes = Mathf.FloorToInt(currentTime / 60);
        int seconds = Mathf.FloorToInt(currentTime % 60);
        if (timerTXT != null)
        {
            timerTXT.text = $" {minutes:00}:{seconds:00}";

            if (currentTime <= 10f)
            {
                timerTXT.color = Color.red;
            }
            else
            {
                timerTXT.color = Color.white;
            }
        }
    }

    private void UpdateScoreUI()
    {
        if (recipeText != null)
        {
            // Add score to the top of recipe instructions
            /*string currentRecipe = recipeText.text;
            recipeText.text = $"Score: {currentScore}\n\n{currentRecipe}";*/
            scoreText.text = $" {currentScore}";
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
            if (sfxSource != null && successClip != null) { sfxSource.PlayOneShot(successClip); }

            // Award points for correct burger if timer is still running
            if (timerRunning)
            {
                currentScore += 100;
                UpdateScoreUI();
            }
            
            Debug.Log("Correct! You built the burger perfectly!");
            imageCorrectBurger.SetActive(true);
            UpdateRecipeUI($"Correct! +100 points\nTotal score: {currentScore}");
            
            // After a short delay, generate a new recipe but don't reset the game
            Invoke("ContinueNextBurger", 0.5f);
        }
        else
        {
            Debug.Log("Incorrect. Your burger doesn't match the recipe.");
            imageWrongBurger.SetActive(true);
            UpdateRecipeUI("Incorrect burger! Try again.\n" + GetRecipeDisplayText());

            if (sfxSource != null && failClip != null) { sfxSource.PlayOneShot(failClip); }

            // Destroy all placed ingredients but keep the plate
            foreach (GameObject ingredient in placedIngredientObjects)
            {
                Destroy(ingredient);
            }
            
            // Clear the ingredient lists
            placedIngredientObjects.Clear();
            placedIngredients.Clear();
            
            // Reset stack height but keep the plate position
            currentStackHeight = 0f;
            lastStackPosition = plateObject.transform.position;
        }
    }

    private void ContinueNextBurger()
    {
        // Clear all ingredients but keep the plate
        foreach (GameObject ingredient in placedIngredientObjects)
        {
            Destroy(ingredient);
        }
        
        // Clear tracking lists
        placedIngredientObjects.Clear();
        placedIngredients.Clear();
        
        // Reset stack height but keep plate position
        currentStackHeight = 0f;
        lastStackPosition = plateObject.transform.position;
        
        // Generate new recipe
        GenerateRandomRecipe();
        UpdateRecipeUI(GetRecipeDisplayText());
    }

    private void StartTimer()
    {
        currentTime = gameDuration;
        timerRunning = true;
        UpdateTimerUI();
    }

    private void GameOver()
    {
        UpdateRecipeUI($"Game Over!\nFinal Score: {currentScore}\n\nPlace plate to start a new game.");
            Invoke("ResetGame", 2f);
    }

    // Call this method to reset the game (can be called from a UI button)
    public void ResetGame()
    {
        currentState = GameState.WaitingForPlate;
        timerRunning = false;
        
        // Destroy all placed ingredients
        foreach (GameObject ingredient in placedIngredientObjects)
        {
            Destroy(ingredient);
        }
        
        // Destroy the plate if it exists
        if (plateObject != null)
        {
            Destroy(plateObject);
            plateObject = null;
        }
        
        // Clear the tracking lists
        placedIngredients.Clear();
        placedIngredientObjects.Clear();
        recipe.Clear();

        imageRecipe.SetActive(false);
        imagePlacePlate.SetActive(true);
        UpdateRecipeUI("Place the plate to start the game!");
    }
}
