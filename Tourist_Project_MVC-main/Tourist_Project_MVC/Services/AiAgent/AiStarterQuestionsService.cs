using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.Services.AiAgent
{
    /// <summary>
    /// Role-based starter questions for the AI chat widget. The role is resolved
    /// server-side from the authentication identity (never hard-coded in the
    /// frontend), so the questions always match who is actually logged in.
    /// </summary>
    public interface IAiStarterQuestionsService
    {
        AiStarterQuestionsVM GetForRole(string role);
    }

    public class AiStarterQuestionsService : IAiStarterQuestionsService
    {
        public AiStarterQuestionsVM GetForRole(string role)
        {
            return role switch
            {
                AiIdentityContext.RoleTourist => new AiStarterQuestionsVM
                {
                    Role = "Tourist",
                    Greeting = "Welcome back! I'm your EGYXPLORE travel assistant. I can plan trips for you, " +
                               "recommend destinations, manage your itinerary, and look up anything about Egypt.",
                    Questions = new List<string>
                    {
                        "Can you help me create a new trip?",
                        "What are the best destinations for my next trip?",
                        "Can you show me my trips and help me improve my itinerary?"
                    }
                },
                AiIdentityContext.RoleSponsor => new AiStarterQuestionsVM
                {
                    Role = "Sponsor",
                    Greeting = "Welcome! I'm your EGYXPLORE business assistant. I can help you manage your " +
                               "branches, rewards and offers, and answer questions about the platform.",
                    Questions = new List<string>
                    {
                        "Can you help me create a new branch?",
                        "Can you show me my branches and their information?",
                        "Can you help me create or update my offers and prices?"
                    }
                },
                AiIdentityContext.RoleAdmin => new AiStarterQuestionsVM
                {
                    Role = "Admin",
                    Greeting = "Welcome! I'm your EGYXPLORE admin assistant. I can manage rewards and destinations, " +
                               "show platform statistics, and help with users and platform content.",
                    Questions = new List<string>
                    {
                        "Can you help me create a new reward?",
                        "Can you give me an overview of the platform and its users?",
                        "Can you help me manage destinations, rewards, and platform content?"
                    }
                },
                _ => new AiStarterQuestionsVM
                {
                    Role = "Guest",
                    Greeting = "Welcome to EGYXPLORE! I'm your travel assistant. Explore Egypt's destinations, " +
                               "get recommendations, or ask me anything about planning your trip.",
                    Questions = new List<string>
                    {
                        "What is EGYXPLORE and what can I do here?",
                        "What are the best Egyptian destinations to visit?",
                        "How can I plan a trip in Egypt?"
                    }
                }
            };
        }
    }
}
