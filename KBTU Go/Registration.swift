//
//  Registration.swift
//  KBTU Go
//
//  Created by Райымбек Омаров on 16.11.2024.
//

import Foundation
import SwiftUI
import Security

// Keychain Helper Functions

func savePasswordToKeychain(password: String) -> Bool {
    let passwordData = password.data(using: .utf8)!
    
    let query: [String: Any] = [
        kSecClass as String: kSecClassGenericPassword,
        kSecAttrAccount as String: "userPassword",  // Unique identifier
        kSecValueData as String: passwordData
    ]
    
    let status = SecItemAdd(query as CFDictionary, nil)
    
    if status == errSecSuccess {
        return true
    } else if status == errSecDuplicateItem {
        // Update if the item already exists
        let attributesToUpdate: [String: Any] = [kSecValueData as String: passwordData]
        let updateStatus = SecItemUpdate(query as CFDictionary, attributesToUpdate as CFDictionary)
        return updateStatus == errSecSuccess
    }
    return false
}

func getPasswordFromKeychain() -> String? {
    let query: [String: Any] = [
        kSecClass as String: kSecClassGenericPassword,
        kSecAttrAccount as String: "userPassword",  // Same identifier
        kSecReturnData as String: true,
        kSecMatchLimit as String: kSecMatchLimitOne
    ]
    
    var result: AnyObject?
    let status = SecItemCopyMatching(query as CFDictionary, &result)
    
    if status == errSecSuccess, let data = result as? Data {
        return String(data: data, encoding: .utf8)
    }
    return nil
}

func deletePasswordFromKeychain() -> Bool {
    let query: [String: Any] = [
        kSecClass as String: kSecClassGenericPassword,
        kSecAttrAccount as String: "userPassword"  // Same identifier
    ]
    
    let status = SecItemDelete(query as CFDictionary)
    return status == errSecSuccess
}

struct RegistrationView: View {
    @State private var email = ""
    @State private var password = ""
    @State private var message = ""

    // Function to send email and password to the backend server
    func sendRegistrationRequest(email: String, password: String) {
        let url = URL(string: "https://ea88-95-57-53-33.ngrok-free.app/api/signup")!
        
        // Create the JSON payload
        let body: [String: Any] = [
            "email": email,
            "password": password
        ]
        
        // Convert the body to JSON data
        guard let jsonData = try? JSONSerialization.data(withJSONObject: body, options: []) else {
            print("Error: Unable to convert body to JSON")
            return
        }
        
        // Create the URLRequest
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.httpBody = jsonData
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        
        // Send the request
        let task = URLSession.shared.dataTask(with: request) { data, response, error in
            DispatchQueue.main.async {
                if let error = error {
                    message = "Error: \(error.localizedDescription)"
                    return
                }
                
                // Handle server response here
                if let data = data {
                    do {
                        // Check for a response status or data
                        let responseObject = try JSONSerialization.jsonObject(with: data, options: [])
                        print("Response: \(responseObject)")
                        
                        // Save password to Keychain after successful registration
                        let success = savePasswordToKeychain(password: password)
                        if success {
                            message = "Registration successful! Password saved securely."
                        } else {
                            message = "Registration successful, but failed to save password."
                        }
                    } catch {
                        message = "Error: Unable to parse server response."
                    }
                }
            }
        }
        
        task.resume()
    }

    var body: some View {
        VStack {
            Text("Registration")
                .font(.largeTitle)
            
            TextField("Email", text: $email)
                .textFieldStyle(RoundedBorderTextFieldStyle())
                .padding()
            
            SecureField("Password", text: $password)
                .textFieldStyle(RoundedBorderTextFieldStyle())
                .padding()
            
            Button(action: {
                sendRegistrationRequest(email: email, password: password)
            }) {
                Text("Register")
                    .fontWeight(.bold)
                    .padding()
                    .background(Color.blue)
                    .foregroundColor(.white)
                    .cornerRadius(10)
            }
            .padding()
            
            Text(message)
                .padding()
                .foregroundColor(.green)
        }
        .padding()
    }
}

struct RegistrationView_Previews: PreviewProvider {
    static var previews: some View {
        RegistrationView()
    }
}
