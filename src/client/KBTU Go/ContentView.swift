//
//  ContentView.swift
//  KBTU Go
//
//  Created by Райымбек Омаров on 16.11.2024.
//


import SwiftUI

struct User {
    var name: String
    var email: String
}

func getUserDetails() -> User {
    // In a real scenario, you would decode the JWT or call an API to fetch user details.
    return User(name: "Raiymbek Omarov", email: "raiymbek@example.com")
}

func deleteJWTFromKeychain() -> Bool {
    let query: [String: Any] = [
        kSecClass as String: kSecClassGenericPassword,
        kSecAttrAccount as String: "userJWT"  // Same identifier
    ]
    
    let status = SecItemDelete(query as CFDictionary)
    return status == errSecSuccess
}

struct ContentView: View {
    @AppStorage("isAuthenticated") private var isAuthenticated: Bool = false
    
//    init() {
//        // Check for JWT in Keychain
//        if let _ = getJWTFromKeychain() {
//            isAuthenticated = true
//        }
//    }
    init(isPreview: Bool = false) {
         if isPreview {
             isAuthenticated = true  // Simulate logged-in state for preview
         } else if let _ = getJWTFromKeychain(tokenType: "accesstoken") {
             isAuthenticated = true
         }
     }
    
    var body: some View {
        Group {
            if isAuthenticated {
                MainView(logoutAction: {
                    let success = deleteJWTFromKeychain()
                    if success {
                        print("JWT deleted successfully")
                    } else {
                        print("Failed to delete JWT")
                    }
                    isAuthenticated = false
                })
            } else {
                AuthenticationView(onAuthenticated: {
                    isAuthenticated = true
                })
            }
        }
    }
}
struct MainView: View {
    @State private var name: String = ""   // State variable for name
        @State private var email: String = ""
    @State private var isSheet1Presented = false
    @State private var isSheet2Presented = false
    let columns = [GridItem(.adaptive(minimum: 150))]
    @State private var user: ProtectedResponse?
    var logoutAction: () -> Void  // Accept logout closure
    
    var body: some View {
        VStack {
            // Main content
            TabView {
                // First Tab: Rides
                VStack {
                    HStack{
                        Spacer()
                        Button(action: {
                            isSheet2Presented=true
                        }){
                            Text("Publish")
                                .frame(width: 120,height:50)
                                
                                .background(Color.blue)
                                .foregroundColor(.white)
                                .cornerRadius(30)
                                
                            
                        } .padding(.trailing, 16)
                            .sheet(isPresented: $isSheet2Presented) {
                                // Content to show in the sheet
                                PublishContent()  
                            }
                    }
                    .padding(.top, 16)
                    LazyVGrid(columns: columns, spacing: 16) {
                        // Your ride buttons
                        RideInfoButton(peopleAssembled: "", destination: "", minutesago: "", rideName: "", status: "", color: .red, company: "" ){
                            isSheet1Presented=true
                        }
                        .sheet(isPresented: $isSheet1Presented){
                            SheetContent(title: "title")
                        }
                    }
                    .padding()
                    Spacer()
                }.onAppear {
                    print("onAppear triggered")
                    PostAPIManager.shared.readposts { response in
                        guard let response = response else {
                            print("Failed to fetch posts or no posts available.")
                            return
                        }
                        print("Posts fetched successfully: \(response)")
                    }
                }
                .tabItem {
                    Label("Rides", systemImage: "car.front.waves.up.fill")
                }
                
                // Second Tab: Profile
                VStack {
                    Text("Name: \(name)")
                        .font(.title)
                        .padding()
                    Text("Email: \(email)")
                        .font(.subheadline)
                        .foregroundColor(.gray)
                        .padding()
                    
                    Button(action: {
                        logoutAction()  // Log out action
                    }) {
                        Text("Log Out")
                            .font(.headline)
                            .foregroundColor(.red)
                            .padding()
                    }
                } .onAppear {
                    APIManager.shared.sendProtectedRequest{ protectedResponse in
                        self.name = protectedResponse?.name ?? ""
                        self.email = protectedResponse?.email ?? ""
                    }
                }
                .tabItem {
                    Label("Profile", systemImage: "person.crop.circle")
                }
            }
        }
    }
  
}

struct AuthenticationView: View {
    @State private var showLogin = true
    var onAuthenticated: () -> Void

    var body: some View {
        VStack {
            if showLogin {
                LoginView(onLoginSuccess: onAuthenticated)
            } else {
                RegistrationView(onRegistrationSuccess: onAuthenticated)
            }

            Button(action: {
                showLogin.toggle()
            }) {
                Text(showLogin ? "Switch to Registration" : "Switch to Login")
                    .font(.headline)
                    .padding()
            }
        }
    }
}
struct RideInfoButton: View {
    let peopleAssembled: String
    let destination: String
    let minutesago: String
    let rideName: String
    let status: String
    let color: Color
    let company: String
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            VStack {
                HStack {
                    Text(destination)
                        .font(.title)
                        .fontWeight(.bold)
                        .foregroundColor(.white)

                    // Add clock icon and "minutes ago" text
                    

                    Text(" \(minutesago) min ago")
                        .font(.subheadline)
                        .foregroundColor(.white)
                    
                    Image(systemName: "clock.arrow.circlepath")
                        .foregroundColor(.white)
                        .font(.subheadline) // Adjust the icon size to match the text
                }

              

               

                Text(rideName)
                    .font(.footnote)
                    .foregroundColor(.white)

                Text(peopleAssembled)
                    .foregroundColor(.white)
        
            }
            .frame(width: 180, height: 180) // Round button size
            .background(
                RoundedRectangle(cornerRadius: 30) // Large corner radius for round shape
                    .fill(color)
                    .opacity(0.7)
            )
            
        }
    }
}

struct SheetContent: View {
    let title: String

    var body: some View {
        VStack {
            Text(title)
                .font(.largeTitle)
            Spacer()
        }
        .padding()
    }
}


struct ContentView_Previews: PreviewProvider {
    static var previews: some View {
        ContentView(isPreview: true)  // Pass `true` to simulate being logged in
    }
}
