#if UNITY_EDITOR

// System
using System;
using System.Collections;
using System.Collections.Generic;

// Unity
using UnityEngine;
using UnityEditor;

namespace GUPS.Editor.Gui
{
    /// <summary>
    /// Unity Editor settings window for the global project wide GuardingPearSoftware Company configuration.
    /// </summary>
    public class CompanyWindow
    {
        /// <summary>
        /// Creates a Unity SettingsProvider for the GuardingPearSoftware Company settings in Project Settings.
        /// Handles GUI rendering, settings persistence, and lifecycle management.
        /// </summary>
        /// <returns>Configured SettingsProvider for Unity's Project Settings integration.</returns>
        [SettingsProvider]
        public static SettingsProvider CreateGuardingPearSoftwareCompanySettingsProvider()
        {
            // Create provider and initialize.
            SettingsProvider var_Provider = new SettingsProvider("Project/GuardingPearSoftware", SettingsScope.Project);

            // Assign the name of the window.
            var_Provider.label = "GuardingPearSoftware";
            var_Provider.titleBarGuiHandler = () =>
            {
                // Create button style for support button
                GUIStyle var_SupportButtonStyle = new GUIStyle("button");
                var_SupportButtonStyle.fontSize = 12;
                var_SupportButtonStyle.fontStyle = FontStyle.Bold;

                GUILayout.BeginHorizontal();

                // Website button
                if (GUILayout.Button(new GUIContent("Website", "Opens the company website in your browser."), var_SupportButtonStyle, GUILayout.MaxWidth(85), GUILayout.MaxHeight(28)))
                {
                    Application.OpenURL("https://www.guardingpearsoftware.com/");
                }

                // Support and bug reporting button
                if (GUILayout.Button(new GUIContent("Support", "Opens your mail program to get some support."), var_SupportButtonStyle, GUILayout.MaxWidth(85), GUILayout.MaxHeight(28)))
                {
                    Application.OpenURL("mailto:guardingpearsoftware@gmail.com?subject=Support%20Request");
                }

                GUILayout.EndHorizontal();
            };

            // Populate the search keywords to enable smart search filtering and label highlighting:
            var_Provider.keywords = new HashSet<string>(new[] { "Guarding", "Pear", "Software" });

            // Register a callback that draws the GUI and handles the interaction with the underlying serialized json settings.
            var_Provider.guiHandler = GetGui;

            // Register a callback for when the window is activated.
            var_Provider.activateHandler = (searchContext, rootElement) =>
            {
                // Init the products.
                InitializeProducts();
            };

            // Register a callback for when the window is deactivated.
            var_Provider.deactivateHandler = () =>
            {
            };

            // Return the provider.
            return var_Provider;
        }

        // Textures
        #region Textures

        /// <summary>
        /// Initializes the product categories and their products.
        /// </summary>
        private static void InitializeProducts()
        {
            // Avoid re-initializing if already populated.
            if (productCategories != null)
            {
                return;
            }

            productCategories = new List<ProductCategory>
            {
                new ProductCategory
                {
                    Title = "Security",
                    Description = "Protect your games and hard work with our powerful security tools, keeping them safe from any threat.",
                    Products = new List<Product>
                    {
                        new Product { Name = "Obfuscator", Tooltip = "Obfuscator Pro was especially developed for Unity to increase your software and game security. Its main goal is to obscure your own source code and also compiled DotNet assemblies from third parties.", Url = "https://assetstore.unity.com/packages/slug/89589", Image = (Texture)EditorGUIUtility.Load("Assets/GUPS/Editor/Resources/Obfuscator_Card.png") },
                        new Product { Name = "Anti Cheat", Tooltip = "Unfortunately, cheating and hacking in games are common. To prevent it, use the plug&play implementation of AntiCheat with included easy-to-follow demos and tests.", Url = "https://assetstore.unity.com/packages/slug/300626", Image = (Texture)EditorGUIUtility.Load("Assets/GUPS/Editor/Resources/AntiCheat_Card.png") },
                    }
                },
                new ProductCategory
                {
                    Title = "Development",
                    Description = "Speed up your development with our powerful tools, designed to make your workflow smoother and more productive.",
                    Products = new List<Product>
                    {
                        new Product { Name = "EasyLocalization", Tooltip = "EasyLocalization is an optimized tool for Unity that simplifies and streamlines the localization process. It offers an uncomplicated way to translate your story for any target audience.", Url = "https://assetstore.unity.com/packages/slug/270639", Image = (Texture)EditorGUIUtility.Load("Assets/GUPS/Editor/Resources/EasyLocalization_Card.png") },
                        new Product { Name = "EasyPerformanceMonitor", Tooltip = "EasyPerformanceMonitor is an in-game performance monitoring tool designed for Unity3d. With real-time monitoring of essential metrics such as FPS, CPU usage, GPU usage, memory usage and more.", Url = "https://assetstore.unity.com/packages/slug/258079", Image = (Texture)EditorGUIUtility.Load("Assets/GUPS/Editor/Resources/EasyPerformanceMonitor_Card.png") },
                        new Product { Name = "EasyPooling", Tooltip = "Boost the performance of your Unity games effortlessly with EasyPooling. Efficient GameObject pooling for smooth gameplay, fewer frame drops and seamless memory management.", Url = "https://assetstore.unity.com/packages/slug/275545", Image = (Texture)EditorGUIUtility.Load("Assets/GUPS/Editor/Resources/EasyPooling_Card.png") },
                        new Product { Name = "Serialization", Tooltip = "A high-performance, Unity-optimized serialization system for saving, loading, and sharing complex game data. Built-in support for compression, encryption, async and cross-platform compatibility.", Url = "https://assetstore.unity.com/packages/slug/124253", Image = (Texture)EditorGUIUtility.Load("Assets/GUPS/Editor/Resources/Serialization_Card.png") }
                    }
                }
            };
        }

        #endregion

        // Gui
        #region Gui

        /// <summary>
        /// A list of product categories to display in the window.
        /// </summary>
        private static List<ProductCategory> productCategories;

        /// <summary>
        /// Defines a product to be displayed in the company window.
        /// </summary>
        private class Product
        {
            public string Name;
            public string Tooltip;
            public string Url;
            public Texture Image;
        }

        /// <summary>
        /// Defines a category for grouping products.
        /// </summary>
        private class ProductCategory
        {
            public string Title;
            public string Description;
            public List<Product> Products;
        }

        /// <summary>
        /// Current scroll position for the settings window's scrollable content area.
        /// </summary>
        private static Vector2 scrollPosition;

        /// <summary>
        /// Renders the complete obfuscator settings GUI interface.
        /// Handles container updates, header rendering, tabbed interface, and auto-save.
        /// </summary>
        /// <param name="_SearchContext">Unity's search context string (currently unused).</param>
        private static void GetGui(String _SearchContext)
        {
            try
            {
                // Display the gui content.
                EditorGUILayout.LabelField("We're a Germany-based company that creates tools to improve security and make game development quicker and simpler.", EditorStyles.wordWrappedLabel);

                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

                // Begin scrollable content area
                scrollPosition = GUILayout.BeginScrollView(scrollPosition);

                // Create transparent button style for header buttons
                GUIStyle var_TopBarButtonStyle = new GUIStyle("button");
                var_TopBarButtonStyle.normal.background = null;
                var_TopBarButtonStyle.active.background = null;

                // The product region
                if (productCategories != null)
                {
                    foreach (var category in productCategories)
                    {
                        EditorGUILayout.LabelField(category.Title, EditorStyles.boldLabel);
                        EditorGUILayout.LabelField(category.Description, EditorStyles.wordWrappedLabel);
                        EditorGUILayout.Space();

                        const int columns = 3;
                        for (int i = 0; i < category.Products.Count; i += columns)
                        {
                            EditorGUILayout.BeginHorizontal();
                            for (int j = 0; j < columns; ++j)
                            {
                                int index = i + j;
                                if (index < category.Products.Count)
                                {
                                    var product = category.Products[index];
                                    EditorGUILayout.BeginVertical(GUILayout.Width(160));

                                    if (GUILayout.Button(new GUIContent("", product.Image, product.Tooltip), GUILayout.MaxWidth(160), GUILayout.MaxHeight(105)))
                                    {
                                        Application.OpenURL(product.Url);
                                    }

                                    GUIStyle labelStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter };
                                    EditorGUILayout.LabelField(new GUIContent(product.Name, product.Tooltip), labelStyle, GUILayout.MaxWidth(160));

                                    EditorGUILayout.EndVertical();
                                }
                            }
                            EditorGUILayout.EndHorizontal();
                        }

                        EditorGUILayout.Space();
                        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                    }
                }

                GUILayout.EndScrollView();
            }
            catch (Exception e)
            {
                Debug.LogError("[GUPS] " + e.ToString());
            }
        }

        #endregion
    }
}

#endif