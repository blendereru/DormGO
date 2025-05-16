//
//  CreatePost.swift
//  KBTU Go
//
//  Created by Райымбек Омаров on 22.11.2024.
//

import Foundation
import SwiftUI
import Security
import CryptoKit
import MapKit
import SwiftUI
import CoreLocation
import Combine

struct Post: Codable {
    let postId: String
    let description: String
    let currentPrice: Double
    let latitude: Double
    let longitude: Double
    let createdAt: String
    let maxPeople: Int
    let creator : ProtectedResponse
    let members: [ProtectedResponse]
}

struct PostsResponse_other: Codable {
    let postsWhereMember: [Post]
}
// Root response model
struct PostsResponse: Codable {
    var yourPosts: [Post]
    var restPosts: [Post]
}
//https://8035-2-135-65-38.ngrok-free.app
let baseURL = URL(string: "http://localhost:8080")! // https://dormgo.azurewebsites.net    http://localhost:8080




func endpoint(_ path: String) -> URL {
    return baseURL.appendingPathComponent(path)
}
class PostAPIManager{
   
    static let shared = PostAPIManager()
    private var isRefreshing = false
    private var pendingRequests: [(Bool) -> Void] = []
    private let lockQueue = DispatchQueue(label: "com.example.PostAPIManager.lock")
    func sendProtectedRequest2(Title:String,Description: String, CurrentPrice: Double, Latitude: Double, Longitude: Double, CreatedAt: String, MaxPeople: Int, completion: @escaping (ProtectedResponse?) -> Void) {
        let url = endpoint("api/post/create")
        
        // Retrieve the JWT token from Keychain
        guard let token = getJWTFromKeychain(tokenType: "access_token") else {
            print("Access token missing. Attempting to refresh token.")
            refreshToken2 { success in
                if success {
                    self.sendProtectedRequest2(
                        Title:Title,
                        Description: Description,
                        CurrentPrice: CurrentPrice,
                        Latitude: Latitude,
                        Longitude: Longitude,
                        CreatedAt: CreatedAt,
                        MaxPeople: MaxPeople,
                        completion: completion
                    )
                } else {
                    print("Unable to refresh token. Exiting.")
                        //    completion(nil)
                }
            }
            return
        }
        print("JWT Token: \(token)")
        
        var request = URLRequest(url: url)
        request.httpMethod = "POST"  // Make sure the method is POST
        
        // Set the JWT token in the Authorization header
        request.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization")
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        
        // Use the dynamic body passed into the function
        let body: [String: Any] = [
            "Title":Title,
            "Description": Description,
            "CurrentPrice": CurrentPrice,
            "Latitude": Latitude,
            "Longitude": Longitude,
            "CreatedAt": CreatedAt,
            "MaxPeople": MaxPeople,
            
        ]
        
        // Convert body to JSON data
        guard let jsonData = try? JSONSerialization.data(withJSONObject: body, options: []) else {
            print("Error: Unable to convert body to JSON")
            return
        }
        
        request.httpBody = jsonData
        
        // Print the headers to terminal
        if let allHeaders = request.allHTTPHeaderFields {
            print("Request Headers: \(allHeaders)")
        }
        
        let task = URLSession.shared.dataTask(with: request) { data, response, error in
            DispatchQueue.main.async {
                if let error = error {
                    print("Error: \(error.localizedDescription)")
                    return
                }
                
                if let httpResponse = response as? HTTPURLResponse {
                    print("HTTP Status Code: \(httpResponse.statusCode)")
                    
                    if let headers = httpResponse.allHeaderFields as? [String: String] {
                        print("Response Headers: \(headers)")
                    }
                    if httpResponse.statusCode == 401 {
                        print("Token expired. Refreshing...")
                        self.refreshToken2 { success in
                            if success {
                                self.sendProtectedRequest2(
                                    Title: Title,
                                    Description: Description,
                                    CurrentPrice: CurrentPrice,
                                    Latitude: Latitude,
                                    Longitude: Longitude,
                                    CreatedAt: CreatedAt,
                                    MaxPeople: MaxPeople,
                                    completion: completion
                                )
                            } else {
                                print("Failed to refresh token.")
                                completion(nil)
                            }
                        }
                        return
                    }
                    
                    if let data = data, let responseString = String(data: data, encoding: .utf8) {
                        print("Response Body: \(responseString)")
                        do {
                            let decoder = JSONDecoder()
                            let protectedResponse = try decoder.decode(ProtectedResponse.self, from: data)
                            print("Email: \(protectedResponse.email)")
                            print("Name: \(protectedResponse.name)")
                            completion(protectedResponse)
                        } catch {
                            print("Error decoding JSON: \(error.localizedDescription)")
                        }
                    } else {
                        print("No data received")
                    }
                }
            }
        }
        
        task.resume()
    }
    func logoutfunc(refreshToken:String,accessToken:String) {
        let url = endpoint("api/signout")
        
        // Retrieve the JWT token from Keychain
        guard let token = getJWTFromKeychain(tokenType: "access_token") else {
            print("Access token missing. Attempting to refresh token.")
            refreshToken2 { success in
                if success {
                    self.logoutfunc(
                        refreshToken: refreshToken, accessToken: accessToken
                    )
                } else {
                    print("Unable to refresh token. Exiting.")
                        //    completion(nil)
                }
            }
            return
        }
        print("JWT Token: \(token)")
        
        var request = URLRequest(url: url)
        request.httpMethod = "DELETE"  // Make sure the method is POST
        
        // Set the JWT token in the Authorization header
        request.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization")
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        
        // Use the dynamic body passed into the function
        let body: [String: Any] = [
            "refreshToken": refreshToken,
            "accessToken":accessToken
        ]
        
        // Convert body to JSON data
        guard let jsonData = try? JSONSerialization.data(withJSONObject: body, options: []) else {
            print("Error: Unable to convert body to JSON")
            return
        }
        
        request.httpBody = jsonData
        
        // Print the headers to terminal
        if let allHeaders = request.allHTTPHeaderFields {
            print("Request Headers: \(allHeaders)")
        }
        
        let task = URLSession.shared.dataTask(with: request) { data, response, error in
            DispatchQueue.main.async {
                if let error = error {
                    print("Error: \(error.localizedDescription)")
                    return
                }
                
                if let httpResponse = response as? HTTPURLResponse {
                    print("HTTP Status Code: \(httpResponse.statusCode)")
                    
                    if let headers = httpResponse.allHeaderFields as? [String: String] {
                        print("Response Headers: \(headers)")
                    }
                    if httpResponse.statusCode == 401 {
                        print("Token expired. Refreshing...")
                        self.refreshToken2 { success in
                            if success {
                                self.logoutfunc(refreshToken: refreshToken, accessToken: accessToken)
                            } else {
                                print("Failed to refresh token.")
                         
                            }
                        }
                        return
                    }
                    
                    if let data = data, let responseString = String(data: data, encoding: .utf8) {
                        print("Response Body: \(responseString)")
                        do {
                            let decoder = JSONDecoder()
                            let protectedResponse = try decoder.decode(ProtectedResponse.self, from: data)
                            print("Email: \(protectedResponse.email)")
                            print("Name: \(protectedResponse.name)")
                    
                        } catch {
                            print("Error decoding JSON: \(error.localizedDescription)")
                        }
                    } else {
                        print("No data received")
                    }
                }
            }
        }
        
        task.resume()
    }
    
    func update(postId: String ,Title: String,Description: String, CurrentPrice: Double, Latitude: Double, Longitude: Double, CreatedAt: String, MaxPeople: Int,Members:[ProtectedResponse], completion: @escaping (ProtectedResponse?) -> Void) {
        let url = endpoint("api/post/update/\(postId)")
        
        // Retrieve the JWT token from Keychain
        guard let token = getJWTFromKeychain(tokenType: "access_token") else {
            print("Access token missing. Attempting to refresh token.")
            refreshToken2 { success in
                if success {
                    self.update(
                        postId: postId,
                        Title:Title,
                        Description: Description,
                        CurrentPrice: CurrentPrice,
                        Latitude: Latitude,
                        Longitude: Longitude,
                        CreatedAt: CreatedAt,
                        MaxPeople: MaxPeople,
                        Members: Members,
                        completion: completion
                    )
                } else {
                    print("Unable to refresh token. Exiting.")
                    completion(nil)
                }
            }
            return
        }
        print("JWT Token: \(token)")
        
        var request = URLRequest(url: url)
        request.httpMethod = "PUT"  // Make sure the method is POST
        
        // Set the JWT token in the Authorization header
        request.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization")
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        let membersDict = Members.map { $0.toDictionary() }
        // Use the dynamic body passed into the function
        let body: [String: Any] = [
            "PostId": postId,
            "Description": Description,
            "Title": Title,
            "CurrentPrice": CurrentPrice,
            "Latitude": Latitude,
            "Longitude": Longitude,
            "CreatedAt": CreatedAt,
            "MaxPeople": MaxPeople,
            "Members": membersDict // Assuming toDictionary is implemented
        ]
        // Print the JSON in a readable format
        if let jsonData = try? JSONSerialization.data(withJSONObject: body, options: .prettyPrinted),
           let jsonString = String(data: jsonData, encoding: .utf8) {
            print("Request Body (JSON): \(jsonString)")
        } else {
            print("Error: Unable to convert body to JSON")
        }
        
        // Convert body to JSON data
        guard let jsonData = try? JSONSerialization.data(withJSONObject: body, options: []) else {
            print("Error: Unable to convert body to JSON")
            return
        }
        
        request.httpBody = jsonData
        
        // Print the headers to terminal
        if let allHeaders = request.allHTTPHeaderFields {
            print("Request Headers: \(allHeaders)")
        }
        
        let task = URLSession.shared.dataTask(with: request) { data, response, error in
            DispatchQueue.main.async {
                if let error = error {
                    print("Error: \(error.localizedDescription)")
                    return
                }
                
                if let httpResponse = response as? HTTPURLResponse {
                    print("HTTP Status Code: \(httpResponse.statusCode)")
                    
                    if let headers = httpResponse.allHeaderFields as? [String: String] {
                        print("Response Headers: \(headers)")
                    }
                    if httpResponse.statusCode == 401 {
                        print("Token expired. Refreshing...")
                        self.refreshToken2 { success in
                            if success {
                                self.sendProtectedRequest2(
                                    Title:Title, Description: Description,
                                    CurrentPrice: CurrentPrice,
                                    Latitude: Latitude,
                                    Longitude: Longitude,
                                    CreatedAt: CreatedAt,
                                    MaxPeople: MaxPeople,
                                    completion: completion
                                )
                            } else {
                                print("Failed to refresh token.")
                         //       completion(nil)
                            }
                        }
                        return
                    }
                    
                    if let data = data, let responseString = String(data: data, encoding: .utf8) {
                        
                        print("Response Body: \(responseString)")
                        do {
                            let decoder = JSONDecoder()
                            let protectedResponse = try decoder.decode(ProtectedResponse.self, from: data)
                            print("Email: \(protectedResponse.email)")
                            print("Name: \(protectedResponse.name)")
                            completion(protectedResponse)
                        } catch {
                            print("Error decoding JSON: \(error.localizedDescription)")
                        }
                    } else {
                        print("No data received")
                    }
                }
            }
        }
        
        task.resume()
    }
    func deletePost(postId: String) {
        // Construct the URL with the postId
        let url = endpoint("api/post/delete/\(postId)")

        // Retrieve the JWT token from Keychain
        guard let token = getJWTFromKeychain(tokenType: "access_token") else {
            print("Access token missing. Attempting to refresh token.")
            refreshToken2 { success in
                if success {
                    self.deletePost(postId: postId) // Retry after refreshing the token
                } else {
                    print("Unable to refresh token. Exiting.")
                }
            }
            return
        }

        print("JWT Token: \(token)")

        // Prepare the request
        var request = URLRequest(url: url)
        request.httpMethod = "DELETE"
        request.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization")
        request.setValue("application/json", forHTTPHeaderField: "Content-Type") // In case the server expects JSON headers

        // Send the request
        let task = URLSession.shared.dataTask(with: request) { data, response, error in
            DispatchQueue.main.async {
                if let error = error {
                    print("Error: \(error.localizedDescription)")
                    return
                }

                if let httpResponse = response as? HTTPURLResponse {
                    print("HTTP Status Code: \(httpResponse.statusCode)")

                    if httpResponse.statusCode == 401 {
                        print("Token expired. Refreshing...")
                        self.refreshToken2 { success in
                            if success {
                                self.deletePost(postId: postId) // Retry with a refreshed token
                            } else {
                                print("Failed to refresh token.")
                            }
                        }
                        return
                    }

                    if let data = data, let responseString = String(data: data, encoding: .utf8) {
                        print("Response Body: \(responseString)")
                        // Handle the response, e.g., success message, or additional info
                        if httpResponse.statusCode == 200 {
                            // Successfully deleted the post
                            print("Successfully deleted the post.")
                        } else {
                            // Handle other status codes as needed
                            print("Error: \(responseString)")
                        }
                    } else {
                        print("No data received")
                    }
                }
            }
        }

        task.resume()
    }
    func join(postId: String) {
        // Construct the URL with the postId
        let url = endpoint("api/post/join/\(postId)")

        // Retrieve the JWT token from Keychain
        guard let token = getJWTFromKeychain(tokenType: "access_token") else {
            print("Access token missing. Attempting to refresh token.")
            refreshToken2 { success in
                if success {
                    self.join(postId: postId) // Retry after refreshing the token
                } else {
                    print("Unable to refresh token. Exiting.")
                }
            }
            return
        }

        print("JWT Token: \(token)")

        // Prepare the request
        var request = URLRequest(url: url)
        request.httpMethod = "PUT"
        request.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization")
        request.setValue("application/json", forHTTPHeaderField: "Content-Type") // In case the server expects JSON headers

        // Send the request
        let task = URLSession.shared.dataTask(with: request) { data, response, error in
            DispatchQueue.main.async {
                if let error = error {
                    print("Error: \(error.localizedDescription)")
                    return
                }

                if let httpResponse = response as? HTTPURLResponse {
                    print("HTTP Status Code: \(httpResponse.statusCode)")

                    if httpResponse.statusCode == 401 {
                        print("Token expired. Refreshing...")
                        self.refreshToken2 { success in
                            if success {
                                self.join(postId: postId) // Retry with a refreshed token
                            } else {
                                print("Failed to refresh token.")
                            }
                        }
                        return
                    }

                    if let data = data, let responseString = String(data: data, encoding: .utf8) {
                        print("Response Body: \(responseString)")
                        // Handle the response, e.g., success message, or additional info
                        if httpResponse.statusCode == 200 {
                            // Successfully joined the post
                            print("Successfully joined the post.")
                        } else {
                            // Handle other status codes as needed
                            print("Error: \(responseString)")
                        }
                    } else {
                        print("No data received")
                    }
                }
            }
        }

        task.resume()
    }
    
    // Chat function: Send a message to a post
    func sendMessageToPost(postId: String, message: String, completion: @escaping (ChatResponse?) -> Void) {
        let url = endpoint("api/chat/\(postId)/messages")

        // Retrieve the JWT token from Keychain
        guard let token = getJWTFromKeychain(tokenType: "access_token") else {
            print("Access token missing. Attempting to refresh token.")
            refreshToken2 { success in
                if success {
                    self.sendMessageToPost(postId: postId, message: message, completion: completion)
                } else {
                    print("Unable to refresh token. Exiting.")
                    completion(nil)
                }
            }
            return
        }
        
        print("JWT Token: \(token)")
        
        var request = URLRequest(url: url)
        request.httpMethod = "POST"  // Make sure the method is POST
        
        // Set the JWT token in the Authorization header
        request.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization")
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        
        let body: [String: Any] = [
            "content": message
        ]
        
        // Convert body to JSON data
        guard let jsonData = try? JSONSerialization.data(withJSONObject: body, options: []) else {
            print("Error: Unable to convert body to JSON")
            return
        }
        
        request.httpBody = jsonData
        
        // Print the headers to terminal
        if let allHeaders = request.allHTTPHeaderFields {
            print("Request Headers: \(allHeaders)")
        }
        
        let task = URLSession.shared.dataTask(with: request) { data, response, error in
            DispatchQueue.main.async {
                if let error = error {
                    print("Error: \(error.localizedDescription)")
                    return
                }
                
                if let httpResponse = response as? HTTPURLResponse {
                    print("HTTP Status Code: \(httpResponse.statusCode)")
                    
                    if httpResponse.statusCode == 401 {
                        print("Token expired. Refreshing...")
                        self.refreshToken2 { success in
                            if success {
                                self.sendMessageToPost(postId: postId, message: message, completion: completion)
                            } else {
                                print("Failed to refresh token.")
                                completion(nil)
                            }
                        }
                        return
                    }
                    
                    if let data = data, let responseString = String(data: data, encoding: .utf8) {
                        print("Response Body: \(responseString)")
                        do {
                            let decoder = JSONDecoder()
                            let chatResponse = try decoder.decode(ChatResponse.self, from: data)
                            completion(chatResponse)
                        } catch {
                            print("Error decoding JSON: \(error.localizedDescription)")
                            completion(nil)
                        }
                    } else {
                        print("No data received")
                        completion(nil)
                    }
                }
            }
        }
        
        task.resume()
    }

    // Chat function: Fetch messages for a specific post
    func fetchMessagesForPost(postId: String, completion: @escaping ([Message]?) -> Void) {
        let url = endpoint("api/chat/\(postId)/messages")

        // Retrieve the JWT token from Keychain
        guard let token = getJWTFromKeychain(tokenType: "access_token") else {
            print("Access token missing. Attempting to refresh token.")
            refreshToken2 { success in
                if success {
                    self.fetchMessagesForPost(postId: postId, completion: completion)
                } else {
                    print("Unable to refresh token. Exiting.")
                    completion(nil)
                }
            }
            return
        }

        print("JWT Token: \(token)")

        var request = URLRequest(url: url)
        request.httpMethod = "GET"  // Fetch messages using GET method

        // Set the JWT token in the Authorization header
        request.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization")
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")

        let task = URLSession.shared.dataTask(with: request) { data, response, error in
            DispatchQueue.main.async {
                if let error = error {
                    print("Error: \(error.localizedDescription)")
                    completion(nil)
                    return
                }

                if let httpResponse = response as? HTTPURLResponse {
                    print("HTTP Status Code: \(httpResponse.statusCode)")

                    if httpResponse.statusCode == 401 {
                        print("Token expired. Refreshing...")
                        self.refreshToken2 { success in
                            if success {
                                self.fetchMessagesForPost(postId: postId, completion: completion)
                            } else {
                                print("Failed to refresh token.")
                                completion(nil)
                            }
                        }
                        return
                    }

                    if let data = data, let responseString = String(data: data, encoding: .utf8) {
                        print("Response Body: \(responseString)")
                        do {
                            let decoder = JSONDecoder()
                            let messages = try decoder.decode([Message].self, from: data)
                            completion(messages)
                        } catch {
                            print("Error decoding JSON: \(error.localizedDescription)")
                            completion(nil)
                        }
                    } else {
                        print("No data received")
                        completion(nil)
                    }
                }
            }
        }

        task.resume()
    }
    func unjoin(postId: String) {
        // Construct the URL with the postId
        let url = endpoint("api/post/unjoin/\(postId)")

        // Retrieve the JWT token from Keychain
        guard let token = getJWTFromKeychain(tokenType: "access_token") else {
            print("Access token missing. Attempting to refresh token.")
            refreshToken2 { success in
                if success {
                    self.join(postId: postId) // Retry after refreshing the token
                } else {
                    print("Unable to refresh token. Exiting.")
                }
            }
            return
        }

        print("JWT Token: \(token)")

        // Prepare the request
        var request = URLRequest(url: url)
        request.httpMethod = "DELETE"
        request.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization")
        request.setValue("application/json", forHTTPHeaderField: "Content-Type") // In case the server expects JSON headers

        // Send the request
        let task = URLSession.shared.dataTask(with: request) { data, response, error in
            DispatchQueue.main.async {
                if let error = error {
                    print("Error: \(error.localizedDescription)")
                    return
                }

                if let httpResponse = response as? HTTPURLResponse {
                    print("HTTP Status Code: \(httpResponse.statusCode)")

                    if httpResponse.statusCode == 401 {
                        print("Token expired. Refreshing...")
                        self.refreshToken2 { success in
                            if success {
                                self.join(postId: postId) // Retry with a refreshed token
                            } else {
                                print("Failed to refresh token.")
                            }
                        }
                        return
                    }

                    if let data = data, let responseString = String(data: data, encoding: .utf8) {
                        print("Response Body: \(responseString)")
                        // Handle the response, e.g., success message, or additional info
                        if httpResponse.statusCode == 200 {
                            // Successfully joined the post
                            print("Successfully joined the post.")
                        } else {
                            // Handle other status codes as needed
                            print("Error: \(responseString)")
                        }
                    } else {
                        print("No data received")
                    }
                }
            }
        }

        task.resume()
    }
    func generateHashedFingerprint(fingerprint: String) -> String? {
        if let data = fingerprint.data(using: .utf8) {
            let hash = SHA256.hash(data: data)
            return hash.compactMap { String(format: "%02x", $0) }.joined()
        }
        return nil
    }
    func refreshToken2(caller: String = #function,completion: @escaping (Bool) -> Void) {
        print("refreshToken2 called from \(caller)")
//        lockQueue.sync {
//             if isRefreshing {
//                 // If already refreshing, queue the request
//                 pendingRequests.append(completion)
//                 return
//             }
//
//             isRefreshing = true
//         }

         // Retrieve tokens from Keychain
         guard let refreshToken = getJWTFromKeychain(tokenType: "refresh_token"),
               let accessToken = getJWTFromKeychain(tokenType: "access_token") else {
             print("Token(s) missing.")
             invokePendingRequests(success: false)
             return
         }

         // Prepare the request
         let refreshURL = endpoint("api/refresh-tokens")
         var request = URLRequest(url: refreshURL)
         request.httpMethod = "PUT"
         request.setValue("application/json", forHTTPHeaderField: "Content-Type")
            // let fingerprint = UIDevice.current.identifierForVendor?.uuidString
         guard let hashedFingerprint = generateHashedFingerprint(fingerprint: fingerprint) else {
         //    invokePendingRequests(success: false)
             return
         }
         let body: [String: Any] = [
             "refreshToken": refreshToken,
             "accessToken": accessToken,
             "visitorId": hashedFingerprint
         ]
         request.httpBody = try? JSONSerialization.data(withJSONObject: body, options: [])

         // Make the request to refresh the token
         let task = URLSession.shared.dataTask(with: request) { data, response, error in
             DispatchQueue.main.async {
//                 self.lockQueue.sync {
//                     self.isRefreshing = false
//                 }

                 if let error = error {
                     print("Error refreshing token: \(error.localizedDescription)")
                  //   self.invokePendingRequests(success: false)
                     return
                 }

                 guard let httpResponse = response as? HTTPURLResponse else {
                     print("Error: Unexpected response.")
                 //    self.invokePendingRequests(success: false)
                     return
                 }

                 switch httpResponse.statusCode {
                 case 200:
                     // Successfully refreshed the token
                     guard let data = data else {
                         print("Error: No data received")
                       //  self.invokePendingRequests(success: false)
                         return
                     }

                     do {
                         clearKeychainItem(tokenType: "access_token")
                         clearKeychainItem(tokenType: "refresh_token")
                         if let responseObject = try JSONSerialization.jsonObject(with: data, options: []) as? [String: Any],
                            let newAccessToken = responseObject["access_token"] as? String,
                            let newRefreshToken = responseObject["refresh_token"] as? String {

                             print("Access Token received: \(newAccessToken)")
                             print("Refresh Token received: \(newRefreshToken)")

                             // Save tokens to Keychain
                             let isAccessTokenSaved = saveJWTToKeychain(token: newAccessToken, tokenType: "access_token")
                             let isRefreshTokenSaved = saveJWTToKeychain(token: newRefreshToken, tokenType: "refresh_token")

                             if isAccessTokenSaved && isRefreshTokenSaved {
                                 print("Both tokens saved successfully!")
                                 completion(true)
                           //      self.invokePendingRequests(success: true)
                             } else {
                                 print("Error saving tokens to Keychain")
                            //     self.invokePendingRequests(success: false)
                             }
                         } else {
                             print("Error: Tokens not found in response")
                         //    self.invokePendingRequests(success: false)
                         }
                     } catch {
                         print("Error: Failed to parse server response - \(error.localizedDescription)")
                   //      self.invokePendingRequests(success: false)
                     }

                 case 401:
                     print("refreshToken2rv ervebfbffsv called from \(caller)")
                     // Refresh token is invalid or expired, log the user out
                     print("Unauthorized: Refresh token is invalid or expired.")

                     // Delete tokens from Keychain
                    

                     // Update `isAuthenticated`
                     UserDefaults.standard.set(false, forKey: "isAuthenticated")

                  //   self.invokePendingRequests(success: false)

                 default:
                     // Handle other HTTP status codes
                     print("Failed to refresh token. Status code: \(httpResponse.statusCode)")
                   //  self.invokePendingRequests(success: false)
                 }
             }
         }
         task.resume()
     }

     private func invokePendingRequests(success: Bool) {
         lockQueue.sync {
             pendingRequests.forEach { $0(success) }
             pendingRequests.removeAll()
         }
     }
    func readposts(completion: @escaping (PostsResponse?) -> Void) {
        let url = endpoint("api/post/read")
        
        guard let token = getJWTFromKeychain(tokenType: "access_token") else {
            print("Access token missing. Attempting to refresh token.")
            refreshToken2 { success in
                if success {
                    self.readposts(completion: completion)
                } else {
                    print("Unable to refresh token. Exiting.")
                    completion(nil)
                }
            }
            return
        }
        
        var request = URLRequest(url: url)
        request.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization")
        
        let task = URLSession.shared.dataTask(with: request) { data, response, error in
            DispatchQueue.main.async {
                if let error = error {
                    print("Network error: \(error.localizedDescription)")
                    completion(nil)
                    return
                }
                
                if let httpResponse = response as? HTTPURLResponse {
                    if httpResponse.statusCode == 401 {
                        print("Tokenтттттттттттт expired. Refreshing...")
                        self.refreshToken2 { success in
                            if success {
                                print("re-read")
                                self.readposts(completion: completion)
                            } else {
                                print("Failed to refresh token.")
                                    completion(nil)
                            }
                        }
                        return
                    } else if httpResponse.statusCode != 200 {
                        print("Request failed with status code: \(httpResponse.statusCode)")
                        completion(nil)
                        return
                    }
                    
                    if let data = data {
                        do {
                            let decoder = JSONDecoder()
                            let postsResponse = try decoder.decode(PostsResponse.self, from: data)
                            //  saveToModel(email: protectedResponse.email, name: protectedResponse.name)
                            completion(postsResponse)
                        } catch {
                            print("Failed to decode response: \(error.localizedDescription)")
                            completion(nil)
                        }
                    } else {
                        print("No data received.")
                        completion(nil)
                    }
                }
            }
        }
        
        // Start the network task
        task.resume() // <-- This line is important!
    }
    func read_other(completion: @escaping (PostsResponse_other?) -> Void) {
        let endpointPath = "/api/post/read"
        var components = URLComponents(url: baseURL, resolvingAgainstBaseURL: true)!
        components.path = endpointPath

        components.queryItems = [
            URLQueryItem(name: "joined", value: "true") // Or "false"
        ]

        let url = components.url!
        print("Final URL: \(url)")
        guard let token = getJWTFromKeychain(tokenType: "access_token") else {
            print("Access token missing. Attempting to refresh token.")
            refreshToken2 { success in
                if success {
                    self.read_other(completion: completion)
                } else {
                    print("Unable to refresh token. Exiting.")
                    completion(nil)
                }
            }
            return
        }
        
        var request = URLRequest(url: url)
        request.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization")
        
        let task = URLSession.shared.dataTask(with: request) { data, response, error in
            DispatchQueue.main.async {
                if let error = error {
                    print("Network error: \(error.localizedDescription)")
                    completion(nil)
                    return
                }
                
                if let httpResponse = response as? HTTPURLResponse {
                    if httpResponse.statusCode == 401 {
                        print("Token expired. Refreshing...")
                        self.refreshToken2 { success in
                            if success {
                                self.read_other(completion: completion)
                            } else {
                                print("Failed to refresh token.")
                          //      completion(nil)
                            }
                        }
                        return
                    } else if httpResponse.statusCode != 200 {
                        print("Request failed with status code: \(httpResponse.statusCode)")
                        completion(nil)
                        return
                    }
                    
                    if let data = data {
                        do {
                            let decoder = JSONDecoder()
                            let posts = try decoder.decode(PostsResponse_other.self, from: data)
                            completion(posts)
                        } catch {
                            print("Failed to decode response: \(error.localizedDescription)")
                            completion(nil)
                        }
                    } else {
                        print("No data received.")
                        completion(nil)
                    }
                }
            }
        }
        
        // Start the network task
        task.resume()
    }
    func readPost(postId: String, completion: @escaping (Post?) -> Void) {
        let url = endpoint("api/post/read/\(postId)") // Adjust the URL to get a specific post
        
        guard let token = getJWTFromKeychain(tokenType: "access_token") else {
            print("Access token missing. Attempting to refresh token.")
            refreshToken2 { success in
                if success {
                    self.readPost(postId: postId, completion: completion)
                } else {
                    print("Unable to refresh token. Exiting.")
                    completion(nil)
                }
            }
            return
        }
        
        var request = URLRequest(url: url)
        request.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization")
        
        let task = URLSession.shared.dataTask(with: request) { data, response, error in
            DispatchQueue.main.async {
                if let error = error {
                    print("Network error: \(error.localizedDescription)")
                    completion(nil)
                    return
                }
                
                if let httpResponse = response as? HTTPURLResponse {
                    if httpResponse.statusCode == 401 {
                        print("Token expired. Refreshing...")
                        self.refreshToken2 { success in
                            if success {
                                self.readPost(postId: postId, completion: completion)
                            } else {
                                print("Failed to refresh token.")
                     //           completion(nil)
                            }
                        }
                        return
                    } else if httpResponse.statusCode != 200 {
                        print("Request failed with status code: \(httpResponse.statusCode)")
                        completion(nil)
                        return
                    }
                    
                    if let data = data {
                        do {
                            let decoder = JSONDecoder()
                            let post = try decoder.decode(Post.self, from: data) // Decode a single Post
                            completion(post)
                        } catch {
                            print("Failed to decode response: \(error.localizedDescription)")
                            completion(nil)
                        }
                    } else {
                        print("No data received.")
                        completion(nil)
                    }
                }
            }
        }
        
        // Start the network task
        task.resume()
    }
}
class LocationManager: NSObject, ObservableObject, CLLocationManagerDelegate {
    private var locationManager = CLLocationManager()
    
    @Published var currentLocation: CLLocationCoordinate2D?
    
    override init() {
        super.init()
        locationManager.delegate = self
        locationManager.desiredAccuracy = kCLLocationAccuracyBest
        locationManager.requestWhenInUseAuthorization()
        locationManager.startUpdatingLocation()
    }
    
    func locationManager(_ manager: CLLocationManager, didUpdateLocations locations: [CLLocation]) {
        if let location = locations.last {
            DispatchQueue.main.async {
                self.currentLocation = location.coordinate
            }
        }
    }
}


struct PublishContent: View {
    @State private var message = ""
    @StateObject private var locationManager = LocationManager()
    // State variables to bind to the form fields
    @State private var title = ""
    @State private var description = ""
    @State private var currentPrice = ""
    @State private var latitude = ""
    @State private var longitude = ""
    @State private var createdAt = ""
    @State private var maxPeople = ""
    @State private var cancellables = Set<AnyCancellable>()

    @State private var name = ""
    @State private var email = ""
    
    @State private var region = MKCoordinateRegion(
         center: CLLocationCoordinate2D(latitude: 43.25566911748583,  longitude: 76.94311304177864),
         span: MKCoordinateSpan(latitudeDelta: 0.002, longitudeDelta: 0.002)
     )
    
    @State private var selectedCoordinate: CLLocationCoordinate2D?
    // Function to send data to the server
    func sendCreateRequest(Title:String,Description: String, CurrentPrice: Double, Latitude: Double, Longitude: Double, CreatedAt: String, MaxPeople: Int) {
        let url = endpoint("api/post/create")

        let body: [String: Any] = [
            "Title":Title,
            "Description": Description,
            "CurrentPrice": CurrentPrice,
            "Latitude": Latitude,
            "Longitude": Longitude,
            "CreatedAt": CreatedAt,
            "MaxPeople":MaxPeople,
  
        ]
        
        guard let jsonData = try? JSONSerialization.data(withJSONObject: body, options: []) else {
            message = "Error: Unable to convert the provided details into JSON format."
            return
        }
        // Convert JSON data to a pretty-printed string
         if let jsonString = String(data: jsonData, encoding: .utf8) {
             // Print the JSON string to the terminal
             print("JSON to send:\n\(jsonString)")
         }
     
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.httpBody = jsonData
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        
        URLSession.shared.dataTask(with: request) { data, response, error in
            DispatchQueue.main.async {
                if let error = error {
                    message = "Error: \(error.localizedDescription). Please check your internet connection and try again."
                    return
                }
                
                if let data = data {
                    do {
                        if let responseObject = try JSONSerialization.jsonObject(with: data, options: []) as? [String: Any] {
                            if responseObject["success"] as? Bool == true {
                                message = "Post creation successful. We are now processing your request."
                                // Proceed with further actions, such as token retrieval
                            } else {
                                message = "Post creation failed: \(responseObject["error"] as? String ?? "Unknown error"). Please review the provided details."
                            }
                        } else {
                            message = "Unexpected response format received. Please try again later."
                        }
                    } catch {
                        message = "Error parsing server response. Please try again later."
                    }
                } else {
                    message = "No response from the server. Please try again later."
                }
            }
        }.resume()
    }
    func getCurrentTime() -> String {
         let formatter = DateFormatter()
         formatter.dateFormat = "yyyy-MM-dd'T'HH:mm:ssZ"
         return formatter.string(from: Date())
     }
    
    var body: some View {
        VStack {
            TextField("Title",text:$title)
                .extensionTextFieldView(roundedCornes: 6, startColor: .white, endColor: .blue)
                .padding()
            TextField("Description", text: $description)
              //  .padding()
                .extensionTextFieldView(roundedCornes: 6, startColor: .white, endColor: .blue)
                .padding()
                
                .textFieldStyle(RoundedBorderTextFieldStyle())
            
            TextField("Current Price", text: $currentPrice)
                .padding()
                .keyboardType(.decimalPad)
                .textFieldStyle(RoundedBorderTextFieldStyle())
            
            MapView(region: $region, selectedCoordinate: $selectedCoordinate, latitude: $latitude, longitude: $longitude)
                         .frame(height: 300)
                         .cornerRadius(20) // Set the corner radius to make the map rounded
                             .shadow(radius: 10)
                         .onAppear {
                             if let currentLocation = locationManager.currentLocation {
                                 // If location is already available, update the map region immediately.
                                 region.center = currentLocation
                                 latitude = String(currentLocation.latitude)
                                 longitude = String(currentLocation.longitude)
                             } else {
                                 // Actively watch for the location to be updated
                                 locationManager.$currentLocation
                                     .receive(on: RunLoop.main)
                                     .sink { location in
                                         if let location = location {
                                             region.center = location
                                             latitude = String(location.latitude)
                                             longitude = String(location.longitude)
                                         }
                                     }
                                     .store(in: &cancellables)
                             }
                         }
            
            TextField("Max People", text: $maxPeople)
                .padding()
                .keyboardType(.numberPad)
                .textFieldStyle(RoundedBorderTextFieldStyle())
            
           
            Button(action: {
            
                        // Fetch the creator details using sendProtectedRequest
                PostAPIManager.shared.sendProtectedRequest2(Title: title, Description: description,
                                      CurrentPrice: Double(currentPrice) ?? 0.0,
                                      Latitude: Double(latitude) ?? 0.0,
                                      Longitude: Double(longitude) ?? 0.0,
                                      CreatedAt: getCurrentTime(),  // Assuming you want to use current time here
                                      MaxPeople: Int(maxPeople) ?? 0
                                      ) { protectedResponse in
                    self.name = protectedResponse?.name ?? ""
                    self.email = protectedResponse?.email ?? ""
                            
                           
                            
                            // Convert text inputs to the required data types
                            if let price = Double(currentPrice),
                               let lat = Double(latitude),
                               let lon = Double(longitude),
                               let maxP = Int(maxPeople) {
                            
                                
                                // Call sendCreateRequest with creator data
                                sendCreateRequest(Title: title, Description: description,
                                                  CurrentPrice: price,
                                                  Latitude: lat,
                                                  Longitude: lon,
                                                  CreatedAt: createdAt,
                                                  MaxPeople: maxP
                                             )
                            }
                        }
                    }) {
                Text("Create Post")
                    .padding()
                    .background(Color.blue)
                    .foregroundColor(.white)
                    .cornerRadius(8)
            }
            .padding()
            
            Text(message)
                .padding()
                .foregroundColor(.red)
        }
        .padding()
    }
}
struct UpdateContent: View {
    let  postId: String
    let member: [ProtectedResponse]
    @State private var message = ""
    @StateObject private var locationManager = LocationManager()
    // State variables to bind to the form fields
    @State private var title = ""
    @State private var description = ""
    @State private var currentPrice = ""
    @State private var latitude = ""
    @State private var longitude = ""
    @State private var createdAt = ""
    @State private var maxPeople = ""
    @State private var cancellables = Set<AnyCancellable>()

    @State private var name = ""
    @State private var email = ""
    
    @State private var region = MKCoordinateRegion(
         center: CLLocationCoordinate2D(latitude: 43.25566911748583,  longitude: 76.94311304177864),
         span: MKCoordinateSpan(latitudeDelta: 0.002, longitudeDelta: 0.002)
     )
    
    @State private var selectedCoordinate: CLLocationCoordinate2D?
    // Function to send data to the server
    func sendUpdateRequest(postId: String ,Description: String, CurrentPrice: Double, Latitude: Double, Longitude: Double, CreatedAt: String, MaxPeople: Int, Members: [ProtectedResponse]) {
        let url = endpoint("api/post/update/\(postId)")

        let body: [String: Any] = [
            "PostId": postId,
            "Description": Description,
            "CurrentPrice": CurrentPrice,
            "Latitude": Latitude,
            "Longitude": Longitude,
            "CreatedAt": CreatedAt,
            "MaxPeople": MaxPeople,
            "Members": Members // Assuming toDictionary is implemented
        ]
        
        guard let jsonData = try? JSONSerialization.data(withJSONObject: body, options: []) else {
            message = "Error: Unable to convert the provided details into JSON format."
            return
        }

        // Debugging: Print the JSON payload before sending
//        if let jsonString = String(data: jsonData, encoding: .utf8) {
//            print("JSON Payload: \(jsonString)")
//        }

        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.httpBody = jsonData
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        
        URLSession.shared.dataTask(with: request) { data, response, error in
            DispatchQueue.main.async {
                if let error = error {
                    message = "Error: \(error.localizedDescription). Please check your internet connection and try again."
                    return
                }
                
                if let data = data {
                    do {
                        if let responseObject = try JSONSerialization.jsonObject(with: data, options: []) as? [String: Any] {
                            if responseObject["success"] as? Bool == true {
                                message = "Post update successful."
                            } else {
                                message = "Post update failed: \(responseObject["error"] as? String ?? "Unknown error")."
                            }
                        } else {
                            message = "Unexpected response format received."
                        }
                    } catch {
                        message = "Error parsing server response."
                    }
                } else {
                    message = "No response from the server."
                }
            }
        }.resume()
    }
    func getCurrentTime() -> String {
         let formatter = DateFormatter()
         formatter.dateFormat = "yyyy-MM-dd'T'HH:mm:ssZ"
         return formatter.string(from: Date())
     }
    
    var body: some View {
        VStack {
            TextField("Title", text:$title)
            TextField("Description", text: $description)
              //  .padding()
                .extensionTextFieldView(roundedCornes: 6, startColor: .white, endColor: .blue)
                .padding()
                
                .textFieldStyle(RoundedBorderTextFieldStyle())
            
            TextField("Current Price", text: $currentPrice)
                .padding()
                .keyboardType(.decimalPad)
                .textFieldStyle(RoundedBorderTextFieldStyle())
            
            MapView(region: $region, selectedCoordinate: $selectedCoordinate, latitude: $latitude, longitude: $longitude)
                         .frame(height: 300)
                         .cornerRadius(20) // Set the corner radius to make the map rounded
                             .shadow(radius: 10)
                         .onAppear {
                             if let currentLocation = locationManager.currentLocation {
                                 // If location is already available, update the map region immediately.
                                 region.center = currentLocation
                                 latitude = String(currentLocation.latitude)
                                 longitude = String(currentLocation.longitude)
                             } else {
                                 // Actively watch for the location to be updated
                                 locationManager.$currentLocation
                                     .receive(on: RunLoop.main)
                                     .sink { location in
                                         if let location = location {
                                             region.center = location
                                             latitude = String(location.latitude)
                                             longitude = String(location.longitude)
                                         }
                                     }
                                     .store(in: &cancellables)
                             }
                         }
            
            TextField("Max People", text: $maxPeople)
                .padding()
                .keyboardType(.numberPad)
                .textFieldStyle(RoundedBorderTextFieldStyle())
            
           
            Button(action: {
                createdAt = getCurrentTime()
            
                        // Fetch the creator details using sendProtectedRequest
                PostAPIManager.shared.update(postId:postId, Title: title,Description: description,
                                      CurrentPrice: Double(currentPrice) ?? 0.0,
                                      Latitude: Double(latitude) ?? 0.0,
                                      Longitude: Double(longitude) ?? 0.0,
                                      CreatedAt: getCurrentTime(),  // Assuming you want to use current time here
                                      MaxPeople: Int(maxPeople) ?? 0,
                                             Members: member
                                            
                                      ) { protectedResponse in
                    self.name = protectedResponse?.name ?? ""
                    self.email = protectedResponse?.email ?? ""
                            
                           
                            
                            // Convert text inputs to the required data types
                            if let price = Double(currentPrice),
                               let lat = Double(latitude),
                               let lon = Double(longitude),
                               let maxP = Int(maxPeople){
                            
                                
                                // Call sendCreateRequest with creator data
                                sendUpdateRequest(postId:postId,
                                                  Description: description,
                                                  CurrentPrice: price,
                                                  Latitude: lat,
                                                  Longitude: lon,
                                                  CreatedAt: createdAt,
                                                  MaxPeople: maxP,
                                                  Members:  member
                                             )
                                
                            }
                        }
                    }) {
                Text("Create Post")
                    .padding()
                    .background(Color.blue)
                    .foregroundColor(.white)
                    .cornerRadius(8)
            }
            .padding()
            
            Text(message)
                .padding()
                .foregroundColor(.red)
        }
        .padding()
    }
}
extension TextField {

    func extensionTextFieldView(roundedCornes: CGFloat, startColor: Color,  endColor: Color) -> some View {
        self
            .padding()
            .background(LinearGradient(gradient: Gradient(colors: [startColor, endColor]), startPoint: .topLeading, endPoint: .bottomTrailing))
            .cornerRadius(roundedCornes)
            .shadow(color: .blue, radius: 10)
    }
}
struct MapView: UIViewRepresentable {
    @Binding var region: MKCoordinateRegion
    @Binding var selectedCoordinate: CLLocationCoordinate2D?
    @Binding var latitude: String
    @Binding var longitude: String
    
    class Coordinator: NSObject, MKMapViewDelegate {
        var parent: MapView
        
        init(parent: MapView) {
            self.parent = parent
        }
        
        @objc func handleTap(_ gesture: UITapGestureRecognizer) {
            let location = gesture.location(in: gesture.view)
            let mapView = gesture.view as! MKMapView
            let coordinate = mapView.convert(location, toCoordinateFrom: mapView)
            
            // Update selected coordinate and individual bindings
            parent.selectedCoordinate = coordinate
            parent.region.center = coordinate
            parent.latitude = String(coordinate.latitude)
            parent.longitude = String(coordinate.longitude)
        }
    }
    
    func makeCoordinator() -> Coordinator {
        Coordinator(parent: self)
    }
    
    func makeUIView(context: Context) -> MKMapView {
        let mapView = MKMapView()
        mapView.delegate = context.coordinator
        mapView.setRegion(region, animated: true)
        mapView.isUserInteractionEnabled = true
        mapView.mapType = .satellite
        // Add a tap gesture recognizer
        let tapGesture = UITapGestureRecognizer(target: context.coordinator, action: #selector(Coordinator.handleTap(_:)))
        mapView.addGestureRecognizer(tapGesture)
        
        return mapView
    }
    
    func updateUIView(_ uiView: MKMapView, context: Context) {
        uiView.setRegion(region, animated: true)
        
        // Clear existing annotations
        uiView.removeAnnotations(uiView.annotations)
        
//        // Add annotation for the selected coordinate
//        if let selectedCoordinate = selectedCoordinate {
//            let annotation = MKPointAnnotation()
//            annotation.coordinate = selectedCoordinate
//            annotation.title = "Selected Location"
//            uiView.addAnnotation(annotation)
//        }
        
        // Add annotation for the user's current location
        if let currentLatitude = Double(latitude),
           let currentLongitude = Double(longitude) {
            let currentLocationAnnotation = MKPointAnnotation()
            currentLocationAnnotation.coordinate = CLLocationCoordinate2D(latitude: currentLatitude, longitude: currentLongitude)
            currentLocationAnnotation.title = "My Location"
            uiView.addAnnotation(currentLocationAnnotation)
        }
    }}
struct MapView2: UIViewRepresentable {
    var latitude: Double
    var longitude: Double
    
    class Coordinator: NSObject, MKMapViewDelegate {
        var parent: MapView2
        
        init(parent: MapView2) {
            self.parent = parent
        }
    }
    
    func makeCoordinator() -> Coordinator {
        return Coordinator(parent: self)
    }
    
    func makeUIView(context: Context) -> MKMapView {
        let mapView = MKMapView()
        mapView.delegate = context.coordinator
        
        // Set a region to show the specific coordinate
        let coordinate = CLLocationCoordinate2D(latitude: latitude, longitude: longitude)
        let region = MKCoordinateRegion(center: coordinate, latitudinalMeters: 1000, longitudinalMeters: 1000)
        mapView.setRegion(region, animated: false)
        mapView.mapType = .satellite
        // Add an annotation for the specific coordinate
        let annotation = MKPointAnnotation()
        annotation.coordinate = coordinate
        annotation.title = ""
        mapView.addAnnotation(annotation)
        
        return mapView
    }
    
    func updateUIView(_ uiView: MKMapView, context: Context) {
        // Update the annotation if necessary (in this case, we don't need to update anything)
    }
}
//extension MapView.Coordinator {
//    @objc func handleTap(_ gesture: UITapGestureRecognizer) {
//        let location = gesture.location(in: gesture.view)
//        let coordinate = (gesture.view as! MKMapView).convert(location, toCoordinateFrom: gesture.view)
//        parent.selectedCoordinate = coordinate
//        parent.latitude = String(coordinate.latitude)
//        parent.longitude = String(coordinate.longitude)
//    }
//}

struct ChatResponse: Codable {
    var message: Message
    
    struct Message: Codable {
        var messageId: String
        var content: String
        var sender: Sender
        var sentAt: String
        
        struct Sender: Codable {
            var email: String
            var name: String
        }
    }
}
