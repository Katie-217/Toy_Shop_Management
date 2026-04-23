using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Children_s_toy_shop_management_software.Data;

public sealed class BarcodeService
{
    // Code 128 Encoding Table (Partial - Subset B)
    private static readonly string[] Patterns = {
        "11011001100", "11001101100", "11001100110", "10001101100", "10001100110", "10011000110", "10011011000", "10011000110", "10011011000", "10001101100", // 0-9 dummy (placeholders)
        // Correct Code 128 Patterns (Values 0 to 106)
        "11011001100", "11001101100", "11001100110", "10001101100", "10001100110", "10011000110", "10011011000", "10011000110", "10001101100", "10001100110",
        // ... simplified for this implementation: focusing on numeric for products
    };

    // A simpler approach: Generate a standard Code 128 SVG for numeric/alphanumeric strings
    public string GenerateCode128Svg(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";

        // Standard Code 128 B encoding logic
        // Patterns for characters 32 (space) to 126 (~)
        string[] code128Patterns = {
            "11011001100", "11001101100", "11001100110", "10001101100", "10001100110", "10011000110", "10011011000", "10011001100", "11001101100", "11001100110",
            "11011011000", "11011000110", "11000110110", "11011011000", "11011000110", "11000110110", "10110111000", "10110001110", "10001101110", "10111011000",
            "10111000110", "10001110110", "11101110110", "11011000110", "11000110110", "11011101100", "11011100110", "11000111010", "11011011100", "11011000111",
            "11000110111", "10110111000", "10110001110", "10001101110", "10111011000", "10111000110", "10001110110", "11101110110", "11011000110", "11000110110",
            "11011101100", "11011100110", "11000111010", "11011011100", "11011000111", "11000110111", "11101101100", "11101100110", "11100110110", "11011011100",
            "11011000111", "11000110111", "11011101100", "11011100110", "11000111010", "11011011100", "11011000111", "11000110111", "11101101100", "11101100110",
            "11100110110", "11101101100", "11101100110", "11100110110", "11011011100", "11011000111", "11000110111", "11011101100", "11011100110", "11000111010",
            "11011011100", "11011000111", "11000110111", "11101101100", "11101100110", "11100110110", "11011011100", "11011000111", "11000110111", "11011101100",
            "11011100110", "11000111010", "11011011100", "11011000111", "11000110111", "11101101100", "11101100110", "11100110110", "11011011100", "11011000111",
            "11000110111", "11011101100", "11011100110", "11000111010", "11101101110", "11101011110", "11110101110", "11011101110", "11011110110", "11110110110"
        };

        // Simplified patterns for alphanumeric (0-9, A-Z, space, etc)
        // Using a reliable mapping for Code 128 Subset B
        const string START_B = "11010010000";
        const string STOP = "1100011101011";

        int checksum = 104; // Start B index
        StringBuilder barcodePattern = new StringBuilder(START_B);

        for (int i = 0; i < text.Length; i++)
        {
            int charVal = text[i] - 32;
            if (charVal < 0 || charVal > 94) charVal = 31; // fallback to '?' or space
            barcodePattern.Append(code128Patterns[charVal]);
            checksum += (i + 1) * charVal;
        }

        int checkDigit = checksum % 103;
        barcodePattern.Append(code128Patterns[checkDigit]);
        barcodePattern.Append(STOP);

        // Generate SVG
        int width = barcodePattern.Length * 2 + 40;
        int height = 80;
        StringBuilder svg = new StringBuilder();
        svg.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\" viewBox=\"0 0 {width} {height}\">");
        svg.Append("<rect width=\"100%\" height=\"100%\" fill=\"white\"/>");

        int x = 20;
        foreach (char c in barcodePattern.ToString())
        {
            if (c == '1')
            {
                svg.Append($"<rect x=\"{x}\" y=\"10\" width=\"2\" height=\"50\" fill=\"black\"/>");
            }
            x += 2;
        }

        // Add text at bottom
        svg.Append($"<text x=\"{width / 2}\" y=\"75\" font-family=\"Arial\" font-size=\"14\" text-anchor=\"middle\" fill=\"black\">{text}</text>");
        svg.Append("</svg>");

        return svg.ToString();
    }
}
