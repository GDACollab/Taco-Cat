using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum CUST_SPECIES { None, Fish, Raven, Sheep, Frog, Capybara }; //species selectable by CreateCustomerOrder
public class Customer: MonoBehaviour
{
    CustomerManager customerManager;
    TacoMakingGameManager tacoGameManager;
    [HideInInspector]
    public CustomerAnimator anim;

    public Taco submittedTaco;

    [Header("Order UI")]
    public OrderBubble orderUI;

    public List<INGREDIENT_TYPE> order; //ingredients in the order
    public int difficulty = 1;
    public CUST_SPECIES species;

    [HideInInspector] public Vector3 prevPos;
    [HideInInspector] public Vector3 targetPos;
    [HideInInspector] public float interpolater;
    [HideInInspector] public float transitionTime; //How long it takes in seconds for the customer to move between positions
    private float currTransitionTime; //Used for keeping track of time during transitions
    [HideInInspector] public float transitionOffset; //The most that a customers transition time can be randomly offset (used to make customers move at diff speeds)
    [HideInInspector] private float transitionOffsetTimer;
    [HideInInspector] public int currPosition;
    [HideInInspector] public Coroutine moveRoutine = null;
    [HideInInspector] public bool hasEndingDialogue;
    [HideInInspector] public bool hasIntroDialgue;
    [HideInInspector] public float dialoguePause;

    // List of possible colors to tint this customer's sprite once their taco is finished.
    // Elements correspond to the values in the scoreType enum in TacoMakingGameManager.cs .
    public List<Color> colorAfterTacoFinished = new List<Color>
    {
        Color.gray,
        new Color(0 / 255.0f, 243 / 255.0f, 27 / 255.0f), // Bright green
        new Color(192 / 255.0f, 255.0f, 101 / 255.0f), // Light green
        new Color(249 / 255.0f, 141 / 255.0f, 0 / 255.0f), // Orange
        Color.red
    };

    private void Awake()
    {
        tacoGameManager = GetComponentInParent<TacoMakingGameManager>();
        customerManager = GetComponentInParent<CustomerManager>();
        anim = GetComponent<CustomerAnimator>();

        species = ChooseRandomSpecies();

    }

    void Start()
    {
        //orderUI.gameObject.SetActive(false);

        Debug.Log("Species init: " + species);

        anim.ChooseSpeciesRig(species);
        order = CreateCustomerOrder(Mathf.Min(3, difficulty));
    }

    public List<INGREDIENT_TYPE> CreateCustomerOrder(int difficulty) 
    {
        Debug.Log("Created Customer Order");

        // get menu from bench manager
        List<INGREDIENT_TYPE> menu = tacoGameManager.benchManager.menu;
        
        List<int> orderLengths = new List<int>();
        switch (difficulty)
        {
            case 1:
                orderLengths = new List<int> {3, 4};
                break;
            case 2:
                orderLengths = new List<int> {3, 4, 4, 4, 5, 5};
                break;
            case 3:
                orderLengths = new List<int> {3, 4, 5, 5, 5, 6, 6};
                break;
            default:
                orderLengths = new List<int> {3, 4};
                break;
        }
        
        int orderLength = orderLengths[Random.Range(0, orderLengths.Count)]; // randomize order length

        // To be returned
        List<INGREDIENT_TYPE> s_order = new List<INGREDIENT_TYPE>(orderLength);

        // Decides on possible ingredients + said element's weight
        List<int> custPreference = new List<int> { 0, 1, 2, 3, 4 };
        // I would like to switch this with calling for the required value (ie getting fish.value)
        // but afaik we don't have that implemented, and I don't want to risk messing with it rn
        switch (species)
        {
            case CUST_SPECIES.Fish: //No fish, 2x sour cream
                custPreference = new List<int> { 0, 1, 3, 4, 4 };
                break;
            case CUST_SPECIES.Raven: //2x fish
                custPreference = new List<int> { 0, 1, 2, 2, 3, 4 };
                break;
            case CUST_SPECIES.Sheep: //2x cabbage
                custPreference = new List<int> { 0, 0, 1, 2, 3, 4 };
                break;
            case CUST_SPECIES.Frog: // 1/2x fish, 2x jalapenos
                custPreference = new List<int> { 0, 0, 1, 1, 2, 3, 3, 3, 3, 4, 4 };
                break;
            case CUST_SPECIES.Capybara: // 1/2x Pico
                custPreference = new List<int> { 0, 0, 1, 2, 2, 3, 3, 4, 4 };
                break;
        }
        Debug.Log(custPreference);

        // Fill list with random items from menu
        for (int i = 0; i < orderLength; i++)
        {
            int randValue = custPreference[Random.Range(0, custPreference.Count)];
            if (s_order.Contains(menu[randValue])) //If item pulled has already been added, all instances of it are removed as future possibilities
            {
                while (custPreference.Contains(randValue)) {
                    custPreference.Remove(randValue);
                    Debug.Log("Removed item "+randValue+" from "+string.Join(",",custPreference));
                }
            } 
            s_order.Add(menu[randValue]);
        }

        return s_order;
    }

    public CUST_SPECIES ChooseRandomSpecies()    //Generates a Random Species
    {
        return (CUST_SPECIES)Random.Range(1,6);
    }

    // << SPAWN ORDER UI BOX >>
    public GameObject ShowBubbleOrder(List<INGREDIENT_TYPE> order)
    {
        orderUI.gameObject.SetActive(true);

        orderUI.order = order;

        orderUI.ShowOrder();

        return null;
    }


    // compares the list of ingredients in the taco submitted and the list of ingredients in the customer's order returns the taco's score
    public SUBMIT_TACO_SCORE ScoreTaco(Taco tacoToScore)
    {
        // [[ INGREDIENT COUNT]] =========================================================
        int ingredientCountDifference = tacoToScore.ingredients.Count - order.Count;
        Debug.Log("Ingredient Count Difference : " + ingredientCountDifference);

        // if no ingredients in taco, fail
        if (tacoToScore.ingredients.Count == 0) { return SUBMIT_TACO_SCORE.FAILED; }

        // if +-2 ingredients in taco than order, fail
        else if (Mathf.Abs(ingredientCountDifference) >= 2) { return SUBMIT_TACO_SCORE.FAILED; }

        // if +-1 ingredients in taco than order, then okay
        else if (Mathf.Abs(ingredientCountDifference) == 1) { return SUBMIT_TACO_SCORE.OKAY; }

        // [[ SAME INGREDIENTS // INGREDIENT ORDER ]] ====================================
        int numSameIngredients = compareIngredients(tacoToScore);
        int correctPlacementCount = compareIngredientOrder(tacoToScore);

        // << PERFECT >> ingredients are the same and order is perfect
        if (numSameIngredients == order.Count && correctPlacementCount == order.Count)
        {
            return SUBMIT_TACO_SCORE.PERFECT;
        }
        // << GOOD >> ingredients are the same, but order is wrong
        else if (numSameIngredients == order.Count && correctPlacementCount != order.Count)
        {
            return SUBMIT_TACO_SCORE.GOOD;
        }
        // << OKAY TACO >>  1 missing/extra ingredients, incorrect order
        else if ((numSameIngredients == order.Count - 1) && correctPlacementCount != order.Count)
        {
            return SUBMIT_TACO_SCORE.OKAY;
        }
        // << FAILED TACO >>
        else
        {
            return SUBMIT_TACO_SCORE.FAILED;
        }
    }

    public int compareIngredients(Taco taco)
    {
        int sameIngredientCount = 0;
        List<INGREDIENT_TYPE> correctOrder = new List<INGREDIENT_TYPE>(order);

        foreach(INGREDIENT_TYPE ingr in taco.ingredients)
        {
            // if (order.Contains(ingr))
            // {
            //     sameIngredientCount++;
            // }
            if (correctOrder.Contains(ingr))
            {
                correctOrder.Remove(ingr);
            }
        }
        
        sameIngredientCount = order.Count-correctOrder.Count;

        return sameIngredientCount;
    }

    public int compareIngredientOrder(Taco taco)
    {
        int correctPlacementCount = 0;

        if (taco.ingredients.Count > order.Count) { Debug.Log("Submitted " + (taco.ingredients.Count - order.Count) + " more Ingredients than order"); }

        // iterate through taco and check order placement
        for (int i = 0; i < order.Count; i++)
        {
            // if taco ingredient is valid
            if (i < taco.ingredients.Count)
            {
                // if taco ingredient is in the same index of customer order, add to count
                if (taco.ingredients[i] == order[i]) { correctPlacementCount++; }
            }
            else
            {
                Debug.Log("Less Ingredients than order");
            }
        }

        return correctPlacementCount;
    }

    //Moves the customer from their previous position to their new position in line
    public void MoveCustomer(Vector3 newPosition)
    {
        interpolater = 0;
        currTransitionTime = 0;
        transitionOffsetTimer = 0;
        prevPos = transform.position;
        targetPos = newPosition;
        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
        }
        moveRoutine = StartCoroutine(MovePosition());
    }

    private IEnumerator MovePosition()
    {
        //Buffer for customers have dialogue
        while (dialoguePause > 0)
        {
            dialoguePause -= Time.deltaTime;
            yield return null;
        }

        //Buffer between different customers moving
        while (transitionOffsetTimer < transitionOffset)
        {
            transitionOffsetTimer += Time.deltaTime;
            yield return null;
        }

        //Stops moving customer once they are at their new position
        float currTransitionTime = 0f;
        while (currTransitionTime < transitionTime)
        {
            float t = currTransitionTime / transitionTime;
            t = t * t * (3f - 2f * t); // curve
            transform.position = Vector3.Lerp(prevPos, targetPos, t);
            currTransitionTime += Time.deltaTime;
            yield return null;
        }
        moveRoutine = null;
    }

    // Changes this customer's sprite appearance based on the score of their taco
    public void CustomerReaction(SUBMIT_TACO_SCORE tacoScore)
    {
        SpriteRenderer mySpriteRenderer = GetComponent<SpriteRenderer>();
        mySpriteRenderer.color = colorAfterTacoFinished[(int)tacoScore];
    }
}
