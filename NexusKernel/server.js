import express from 'express';
import cors from 'cors';
import { GoogleGenAI } from '@google/genai';
import dotenv from 'dotenv';
import fs from 'fs';

dotenv.config();

const app = express();
app.use(cors());
app.use(express.json({ limit: '50mb' }));

const port = process.env.PORT || 5005;

// The user must provide a GEMINI_API_KEY in the .env file
const ai = new GoogleGenAI({}); 

console.log('🧠 Nexus Neural Kernel Booting...');

app.get('/health', (req, res) => {
    res.status(200).json({ status: 'Warm', version: '16.5' });
});

app.post('/prompt', async (req, res) => {
    const { prompt, systemInstruction, history } = req.body;

    if (!process.env.GEMINI_API_KEY) {
        return res.status(500).send("[red]KERNEL ERROR:[/] GEMINI_API_KEY is missing in NexusKernel/.env\nPlease add it to enable Instant Streaming.");
    }

    res.setHeader('Content-Type', 'text/plain');
    res.setHeader('Transfer-Encoding', 'chunked');

    try {
        let contents = [];
        
        // Map history to Google GenAI format
        if (history && Array.isArray(history)) {
            for (const turn of history) {
                contents.push({
                    role: turn.Role === 'user' ? 'user' : 'model',
                    parts: [{ text: turn.Content }]
                });
            }
        }

        // Add the current prompt
        contents.push({
            role: 'user',
            parts: [{ text: prompt }]
        });

        const responseStream = await ai.models.generateContentStream({
            model: 'gemini-2.5-flash',
            contents: contents,
            config: {
                systemInstruction: systemInstruction || "You are the Nexus Neural Kernel.",
                temperature: 0.7
            }
        });

        for await (const chunk of responseStream) {
            res.write(chunk.text);
        }
        res.end();
    } catch (error) {
        console.error("Kernel Error:", error);
        res.write(`\n[red]KERNEL EXCEPTION:[/] ${error.message}`);
        res.end();
    }
});

app.listen(port, () => {
    console.log(`⚡ Neural Kernel active on http://localhost:${port}`);
    console.log(`Waiting for Hub connection...`);
});
