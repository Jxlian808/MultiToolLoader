   M S B A M L   `   `   `  ����  MultiToolLoader/    )MultiToolLoader.Controls.CustomMessageBox   5   	   W SPresentationCore, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\ XPresentationFramework, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35e aSystem.Windows.Controls.Ribbon, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089R NWindowsBase, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35F 9http://schemas.microsoft.com/winfx/2006/xaml/presentation     5   	   R NSystem.Xaml, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     5   	   $
��Auto��5   	   $	��400��5   	     � SizeToContent$  Height=�5   	    � WindowStartupLocation$ CenterScreen=�5   	    � WindowStyle$
 None=�5	   	    � AllowsTransparency$
 True��5
   	   
������  � Title 5   	   �� *Title+.��5      ��  �� CornerRadius$ 12��6   $��1q�5      $��10q�5      ��5   
   �� 5       �� 
StartPoint$	 0,0&�6"    �� EndPoint$	 1,1&�63   .��5      �  � Color$ #6366F1��6   	 � Offset$	 0Q�6/   � 5      $ #8B5CF6��6   $	 1Q�6/   5      ��5   
   �� 5      $	 0,0&�6"   $	 1,1&�63   .��5      � $ #1E1B4B��6   $	 0Q�6/   � 5      $ #2D3158��6   $	 1Q�6/   5      
 J� Effect
 5   
   3  -System.Windows.Media.Effects.DropShadowEffect  5         
BlurRadius$ 15Q�6      	Direction$	 315Q�5         ShadowDepth$ 5Q�5         Opacity$	 0.2Q�5       ._�5#   
   � $��20q�6    � RowDefinitions 5$      �� 5%      $
��Auto �6    �� 5&      $��* �6    �� 5'      $
��Auto �6    .+�5+      �� $��18(�5,      $
��Bold"�5-      
�������$��0,0,0,15q�5/      ��5+      �� *Title+5/      �� 52      $��1��6   $
��Wrap=�54      
�������$��0,0,0,20q�56      ��53      �� *	Message+56      �� 59      $��2��6    �� Orientation$ 
Horizontal=�5:      $��Right=�5;      .+�5<      �� -   5        $��OK��5<         ActionButton#��[  5>      $��80��5?      $��0,0,10,0q�5@      �� 5B      -   5        $��	Abbrechen��5B      #��[  5D      $��80��5E      y�5F      �� *
ShowCancel+   BooleanToVisibilityConverter �� 	Converter# [ 5G      5H      5I      5J      