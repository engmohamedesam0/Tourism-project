---
title: AI Chat Assistant
description: Chat with the Gemini-powered assistant to ask about destinations, trips, and platform features.
order: 5
---

# AI Chat Assistant

The floating **AI Assistant** button in the bottom-right corner opens a chat panel backed by **AiChatController** and **AiChatService**. It uses the Gemini API to understand natural language and answer questions in context.

## Asking questions

- Click the **Stars** button to open the assistant.
- Type questions like *“What are the best museums in Luxor?”* or *“Help me plan a 3-day Cairo itinerary.”*
- The assistant references live destination and sponsor data when available.

## Creating a trip with AI

- Ask the assistant to create a trip plan and it can call the trip creation endpoint directly.
- Review the draft in **Trip**, then edit stops manually before saving.

### Attachments

- Send images or voice notes by using the attachment controls in the chat panel.
- The assistant can describe uploaded photos and suggest relevant destinations or missions based on visual cues.

> **Note:** Chat history is stored per session in `ChatSession` so you can resume conversations later. Clearing the chat removes saved history from your current browser only.

## When the assistant can’t help

If the AI cannot resolve a question, use the **Support Tickets** page to contact the human support team. Include a screenshot of the chat conversation to speed up resolution.

> **Tip:** Be specific in your requests — mentioning a governorate, activity type, or budget tier produces much better suggestions than broad questions.
