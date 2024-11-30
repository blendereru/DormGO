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
    @State private var user: ProtectedResponse
    @State private var posts: PostsResponse

//    init() {
//        // Check for JWT in Keychain
//        if let _ = getJWTFromKeychain() {
//            isAuthenticated = true
//        }
//    }
    init(isPreview: Bool = false) {
           // Initialize the `user` and `posts` with default values
           if isPreview {
               _user = State(initialValue: ProtectedResponse(email: "preview@example.com", name: "Preview User"))
               _posts = State(initialValue: PostsResponse(yourPosts: [], restPosts: []))  // Simulated empty posts for preview
               isAuthenticated = true  // Simulate logged-in state for preview
           } else if let _ = getJWTFromKeychain(tokenType: "accesstoken") {
               _user = State(initialValue: ProtectedResponse(email: "user@example.com", name: "Authenticated User"))
               _posts = State(initialValue: PostsResponse(yourPosts: [], restPosts: []))  // Add real posts if necessary
               isAuthenticated = true
           } else {
               _user = State(initialValue: ProtectedResponse(email: "", name: ""))
               _posts = State(initialValue: PostsResponse(yourPosts: [], restPosts: []))  // Empty posts if not authenticated
               isAuthenticated = false
           }
       }
    
    var body: some View {
        Group {
            if isAuthenticated {
                MainView(user:user,posts:posts, logoutAction: {
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
    @StateObject private var signalRManager = SignalRManager()  // StateObject for SignalR
    @State private var isSheet2Presented = false
    let columns = [GridItem(.adaptive(minimum: 150))]
    @State private var user: ProtectedResponse
    @State private var posts: PostsResponse = PostsResponse(yourPosts: [], restPosts: [])
    var logoutAction: () -> Void  // Accept logout closure
    
    init(user: ProtectedResponse, posts: PostsResponse, logoutAction: @escaping () -> Void) {
        _user = State(initialValue: user)
        _posts = State(initialValue: posts)
        self.logoutAction = logoutAction
    }
    
    var body: some View {
        VStack {
            // Main content
            TabView {
                // First Tab: Rides
                VStack {
                    HStack {
                        Spacer()
                        Button(action: {
                            isSheet2Presented = true
                        }) {
                            Text("Publish")
                                .frame(width: 120, height: 50)
                                .background(Color.blue)
                                .foregroundColor(.white)
                                .cornerRadius(30)
                        }
                        .padding(.trailing, 16)
                        .sheet(isPresented: $isSheet2Presented) {
                            PublishContent()
                                .onDisappear {
                                    print("Publish Content sheet was dismissed")
                                    // Trigger SignalR to refresh posts when PublishContent sheet is dismissed
                                    signalRManager.startConnection() // Ensure SignalR is active
                                }
                        }
                    }
                    .padding(.top, 16)

                    if !signalRManager.posts.isEmpty {
                        VStack(alignment: .leading, spacing: 16) {
                            // Section Header for "Your Posts"
                            Text("Your Posts")
                                .font(.headline)
                                .padding(.vertical, 8)

                            // Grid for "Your Posts"
                            LazyVGrid(columns: columns, spacing: 16) {
                                ForEach(signalRManager.posts, id: \.createdAt) { post in
                                    RideInfoButton(
                                        peopleAssembled: "\(post.members.count)/\(post.maxPeople)",  // Count the number of members
                                        destination: post.description,
                                        minutesago: calculateMinutesAgo(from: post.createdAt),
                                        rideName: "Price: \(post.currentPrice)₸",
                                        status: "Active",
                                        color: .blue,
                                        company: post.members.first?.name ?? "Unknown"  // Use the first member's name, or "Unknown" if there are no members
                                    ) {
                                        isSheet1Presented = true
                                    }
                                    .sheet(isPresented: $isSheet1Presented) {
                                        SheetContent(title: "title")
                                            .onDisappear {
                                                // SignalRManager will handle real-time post updates
                                            }
                                    }
                                }
                            }
                            .padding()
                        }
                    }

                    Spacer()
                }
                .onAppear {
                    print("onAppear triggered")
                    // Initial fetching is now handled by SignalRManager (real-time updates)
                    signalRManager.startConnection()
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
                }
                .onAppear {
                    APIManager.shared.sendProtectedRequest { protectedResponse in
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
    
    private func calculateMinutesAgo(from createdAt: String) -> String {
        let dateFormatter = ISO8601DateFormatter()
        guard let postDate = dateFormatter.date(from: createdAt) else { return "0" }
        let minutesAgo = Int(Date().timeIntervalSince(postDate) / 60)
        return "\(minutesAgo)"
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
