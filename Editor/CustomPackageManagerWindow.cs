using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace HCG.PackageManagerEditor
{
    public class CustomPackageManagerWindow : EditorWindow
    {
        // Define your Git URLs here
        private readonly string[] _gitUrls =
        {
            "https://github.com/Homecooked-Games-Git/hcg-observerservice.git",
            "https://github.com/Homecooked-Games-Git/hcg-extensions.git"
        };

        // Add menu named "Custom Package Manager" to the Window menu
        [MenuItem("HCTools/Package Manager")]
        public static void ShowWindow()
        {
            // Show existing window instance. If one doesn't exist, make one.
            GetWindow(typeof(CustomPackageManagerWindow));
        }

        private void OnGUI()
        {
            GUILayout.Label("Custom Package Manager", EditorStyles.boldLabel);

            foreach (var gitUrl in _gitUrls)
            {
                // Extract the relevant part from the Git URL
                var packageName = ExtractPackageName(gitUrl);

                // Create a button for each package with the extracted name
                if (GUILayout.Button(packageName))
                {
                    AddPackageFromGitUrl(gitUrl);
                }
            }
        }

// Method to extract the package name from the Git URL
        private static string ExtractPackageName(string gitUrl)
        {
            // Use String methods to extract the part of the URL after the last '/' and before '.git'
            var startIndex = gitUrl.LastIndexOf('/') + 1;
            var endIndex = gitUrl.IndexOf(".git", startIndex, StringComparison.Ordinal);
            var packageName = gitUrl.Substring(startIndex, endIndex - startIndex);
            return packageName;
        }


        private static List<string> ExtractAllDependencies(string errorMessage)
        {
            var dependencies = new List<string>();
            // This regex pattern is designed to capture package names in the format "com.anything.anything@version"
            const string pattern = @"com\.([a-zA-Z0-9_.-]+) \(dependency\): Package \[([a-zA-Z0-9_.-]+)@[0-9.]+\] cannot be found";
            var matches = Regex.Matches(errorMessage, pattern);

            foreach (Match match in matches)
            {
                if (!match.Success) continue;
                // Extracting the part after "com.hcg." and before the "@" symbol, which should be the package short name
                var packageName = match.Groups[1].Value;
                // Debug.Log(packageName);
                var packageShortName = packageName.Split('.')[1]; // Assuming the format is always "com.hcg.[name]"
                // Debug.Log(packageShortName);
                dependencies.Add(packageShortName);
            }

            return dependencies; // Returns a list of all extracted package short names
        }

        private static void AddPackageFromGitUrl(string gitUrl, Action onSuccess = null)
        {
            var request = Client.Add(gitUrl);
            EditorApplication.update += Progress;
            return;

            void Progress()
            {
                if (!request.IsCompleted) return;

                EditorApplication.update -= Progress;

                switch (request.Status)
                {
                    case StatusCode.Success:
                        // Debug.Log($"Successfully added package {request.Result.packageId}");
                        onSuccess?.Invoke(); // Invoke the success callback, if any
                        break;
                    // Debug.LogError($"Failed to add package from {gitUrl}. Error: {request.Error.message}");
                    case StatusCode.Failure when !request.Error.message.Contains("cannot be found"):
                        return;
                    case StatusCode.Failure:
                    {
                        var dependencies = ExtractAllDependencies(request.Error.message);
                        TryAddDependencies(dependencies, () => AddPackageFromGitUrl(gitUrl, onSuccess)); // Retry the original package after dependencies are resolved
                        break;
                    }
                }
            }
        }
        
        private static void TryAddDependencies(List<string> dependencies, Action onAllDependenciesAdded)
        {
            if (dependencies.Count == 0)
            {
                onAllDependenciesAdded?.Invoke();
                return;
            }

            var dependency = dependencies[0];
            dependencies.RemoveAt(0);

            // Construct the Git URL for the dependency using the known pattern
            var gitUrlForDependency = $"https://github.com/Homecooked-Games-Git/hcg-{dependency}.git";

            // Recursively add the dependency, and when successful, attempt the next dependency or complete the process
            AddPackageFromGitUrl(gitUrlForDependency, () =>
            {
                if (dependencies.Count > 0)
                {
                    TryAddDependencies(dependencies, onAllDependenciesAdded);
                }
                else
                {
                    onAllDependenciesAdded?.Invoke();
                }
            });
        }
    }
}
